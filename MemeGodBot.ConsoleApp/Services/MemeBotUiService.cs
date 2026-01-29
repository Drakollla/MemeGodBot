using MemeGodBot.ConsoleApp.Models;
using MemeGodBot.ConsoleApp.Models.Context;
using MemeGodBot.ConsoleApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace MemeGodBot.ConsoleApp.Services
{
    public class MemeBotUiService
    {
        private readonly MemeManager _memeService;
        private readonly MemeDbContext _db;
        private readonly ILogger<MemeBotUiService> _logger;

        public MemeBotUiService(MemeManager memeService, 
                              MemeDbContext db, 
                              ILogger<MemeBotUiService> logger)
        {
            _memeService = memeService;
            _db = db;
            _logger = logger;
        }

        public async Task OnStartAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Формирую кнопки для пользователя {Id}", message.Chat.Id);

                var keyboard = new ReplyKeyboardMarkup(new[]
                {
                    new[] { new KeyboardButton("🎲 Дай мем") },
                    new[] { new KeyboardButton("📊 Статистика") }
                })
                {
                    ResizeKeyboard = true
                };

                await bot.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Привет! Я нейро-мемный бот. Жми на кнопку, а я подберу мем!",
                    replyMarkup: keyboard,
                    cancellationToken: ct
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в OnStartAsync");
            }
        }

        public async Task OnGetMemeAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            try
            {
                var (memeId, path) = await _memeService.GetRecommendationAsync(chatId);

                if (string.IsNullOrEmpty(path) || memeId == 0)
                {
                    await bot.SendMessage(
                        chatId: chatId,
                        text: "😔 Похоже, ты посмотрел все мемы, которые у меня были! \n\nПопробуй зайти позже или подожди, пока я просканирую новые каналы.",
                        cancellationToken: ct
                    );
                    return;
                }

                if (!File.Exists(path))
                {
                    await bot.SendMessage(chatId, "Мем был удален из хранилища. Попробуй еще раз!", cancellationToken: ct);
                    return;
                }

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🔥 Годно", $"like:{memeId}"),
                        InlineKeyboardButton.WithCallbackData("💩 Баян", $"dislike:{memeId}")
                    }
                });

                using var stream = File.OpenRead(path);
                await bot.SendPhoto(
                    chatId: chatId,
                    photo: InputFile.FromStream(stream),
                    replyMarkup: inlineKeyboard,
                    cancellationToken: ct
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выдаче мема");
                await bot.SendMessage(chatId, "Произошла ошибка при поиске мема. Попробуй позже.", cancellationToken: ct);
            }
        }

        public async Task OnReactionAsync(ITelegramBotClient bot, CallbackQuery callback, CancellationToken ct)
        {
            if (callback.Data == null || callback.Message == null)
                return;

            var parts = callback.Data.Split(':');
            var action = parts[0];
            var memeId = ulong.Parse(parts[1]);
            var userId = callback.Message.Chat.Id;
            var isLiked = action == "like";
            var existing = await _db.Reactions.FirstOrDefaultAsync(r => r.UserId == userId && r.QdrantMemeId == (long)memeId, ct);
            
            if (existing != null)
                existing.IsLiked = isLiked;
            else
            {
                _db.Reactions.Add(new UserMemeReaction
                {
                    UserId = userId,
                    QdrantMemeId = (long)memeId,
                    IsLiked = isLiked
                });
            }

            await _db.SaveChangesAsync(ct);
            await bot.AnswerCallbackQuery(callback.Id, $"Принято: {(isLiked ? "🔥" : "💩")}", cancellationToken: ct);
            await bot.EditMessageReplyMarkup(userId, callback.Message.MessageId, replyMarkup: null, cancellationToken: ct);
        }

        public async Task OnStatsAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var likes = await _db.Reactions.CountAsync(r => r.UserId == chatId && r.IsLiked, ct);
            var dislikes = await _db.Reactions.CountAsync(r => r.UserId == chatId && !r.IsLiked, ct);

            await bot.SendMessage(chatId, $"Твоя статистика:\n🔥 Лайков: {likes}\n💩 Дизлайков: {dislikes}\nЧем больше ты оцениваешь, тем точнее я подбираю мемы!", cancellationToken: ct);
        }
    }
}
