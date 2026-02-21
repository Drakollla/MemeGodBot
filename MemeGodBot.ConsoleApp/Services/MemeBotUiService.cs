using MemeGodBot.ConsoleApp.Abstractions;
using MemeGodBot.ConsoleApp.Helpers;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace MemeGodBot.ConsoleApp.Services
{
    public class MemeBotUiService
    {
        private readonly IMemeManager _memeManager;
        private readonly IReactionService _reactionService;
        private readonly ILogger<MemeBotUiService> _logger;

        public MemeBotUiService(IMemeManager memeManager,
                                IReactionService reactionService,
                                ILogger<MemeBotUiService> logger)
        {
            _memeManager = memeManager;
            _reactionService = reactionService;
            _logger = logger;
        }

        public async Task OnStartAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
        {
            try
            {
                var keyboard = CreateMainMenuKeyboard();
                await bot.SendMessage(
                    chatId: message.Chat.Id,
                    text: BotConstants.Messages.Welcome,
                    replyMarkup: keyboard,
                    cancellationToken: ct
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnStartAsync");
            }
        }

        public async Task OnGetMemeAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            try
            {
                var (memeId, path) = await _memeManager.GetRecommendationAsync(chatId);

                if (!IsValidMeme(memeId, path))
                {
                    await SendNoMemesMessageAsync(bot, chatId, ct);
                    return;
                }

                if (!File.Exists(path))
                {
                    await bot.SendMessage(chatId, BotConstants.Messages.MemeDeleted, cancellationToken: ct);
                    return;
                }

                await SendMemePhotoAsync(bot, chatId, path, memeId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting meme");
                await bot.SendMessage(chatId, BotConstants.Messages.Error, cancellationToken: ct);
            }
        }

        private bool IsValidMeme(ulong memeId, string path) =>
            memeId != 0 && !string.IsNullOrEmpty(path);

        private async Task SendNoMemesMessageAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            await bot.SendMessage(chatId: chatId,
                text: BotConstants.Messages.NoMemes,
                cancellationToken: ct
            );
        }

        private async Task SendMemePhotoAsync(ITelegramBotClient bot, long chatId, string path, ulong memeId, CancellationToken ct)
        {
            var keyboard = CreateReactionKeyboard(memeId);

            using var stream = File.OpenRead(path);
            await bot.SendPhoto(
                chatId: chatId,
                photo: InputFile.FromStream(stream),
                replyMarkup: keyboard,
                cancellationToken: ct
            );
        }

        private (string Action, ulong MemeId) ParseCallbackData(string data)
        {
            var parts = data.Split(':');
            return (parts[0], ulong.Parse(parts[1]));
        }

        public async Task OnSearchMemeAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
        {
            var chatId = message.Chat.Id;
            var query = message.Text;

            if (string.IsNullOrWhiteSpace(query))
            {
                await bot.SendMessage(chatId, "Напиши текст для поиска, например: 'кот в сапогах'", cancellationToken: ct);
                return;
            }

            await bot.SendChatAction(chatId, Telegram.Bot.Types.Enums.ChatAction.UploadPhoto, cancellationToken: ct);

            var paths = await _memeManager.SearchMemesByTextAsync(query, limit: 3);

            if (!paths.Any())
            {
                await bot.SendMessage(chatId, "Ничего похожего не нашел...", cancellationToken: ct);
                return;
            }

            var bestMemePath = paths.First();
            using var stream = File.OpenRead(bestMemePath);

            await bot.SendMessage(chatId, $"Вот что нашел по запросу: \"{query}\"", cancellationToken: ct);

            await bot.SendPhoto(
                chatId: chatId,
                photo: InputFile.FromStream(stream),
                caption: "Похоже?",
                cancellationToken: ct
            );
        }

        private ReplyKeyboardMarkup CreateMainMenuKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new[] { new KeyboardButton(BotConstants.Buttons.GetMeme) },
                new[] { new KeyboardButton(BotConstants.Buttons.Stats) }
            })
            {
                ResizeKeyboard = true
            };
        }

        private InlineKeyboardMarkup CreateReactionKeyboard(ulong memeId)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(BotConstants.Buttons.Like, $"{BotConstants.Callbacks.Like}:{memeId}"),
                    InlineKeyboardButton.WithCallbackData(BotConstants.Buttons.Dislike, $"{BotConstants.Callbacks.Dislike}:{memeId}")
                }
            });
        }

        public async Task OnReactionAsync(ITelegramBotClient bot, CallbackQuery callback, CancellationToken ct)
        {
            if (callback.Data == null || callback.Message == null)
                return;

            try
            {
                var (action, memeId) = ParseCallbackData(callback.Data);
                var userId = callback.Message.Chat.Id;
                var isLiked = action == BotConstants.Callbacks.Like;

                await _reactionService.AddReactionAsync(userId, memeId, isLiked, ct);

                await bot.AnswerCallbackQuery(callback.Id, $"Принято: {(isLiked ? "🔥" : "💩")}", cancellationToken: ct);
                await bot.EditMessageReplyMarkup(userId, callback.Message.MessageId, replyMarkup: null, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reaction");
            }
        }

        public async Task OnStatsAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var (likes, dislikes) = await _reactionService.GetUserStatsAsync(chatId, ct);

            await bot.SendMessage(chatId, $"Статистика: {likes} / {dislikes}", cancellationToken: ct);
        }
    }
}
