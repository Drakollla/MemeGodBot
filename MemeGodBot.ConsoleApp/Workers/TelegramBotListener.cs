using MemeGodBot.ConsoleApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MemeGodBot.ConsoleApp.Workers
{
    public class TelegramBotListener : BackgroundService
    {
        private readonly ILogger<TelegramBotListener> _logger;
        private readonly IConfiguration _config;
        private readonly IServiceScopeFactory _scopeFactory;
        private TelegramBotClient? _botClient;

        public TelegramBotListener(ILogger<TelegramBotListener> logger, 
                                 IConfiguration config, 
                                 IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _config = config;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var token = _config["Bot:Token"];
            
            if (string.IsNullOrWhiteSpace(token))
                return;

            _botClient = new TelegramBotClient(token);

            _botClient.StartReceiving(
                updateHandler: HandleBotUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: new ReceiverOptions { AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery] },
                cancellationToken: stoppingToken);

            var me = await _botClient.GetMe(stoppingToken);
            _logger.LogInformation("Бот-интерфейс запущен: {BotName}", me.FirstName);

            await Task.Delay(-1, stoppingToken);
        }

        private async Task HandleBotUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var botLogic = scope.ServiceProvider.GetRequiredService<MemeBotUiService>();

            try
            {
                if (update.CallbackQuery is { } callback)
                {
                    await botLogic.OnReactionAsync(client, callback, ct);
                    return;
                }

                if (update.Message is { } message && message.Text is { } text)
                {
                    if (text.StartsWith("/start"))
                        await botLogic.OnStartAsync(client, message, ct);
                    else if (text == "🎲 Дай мем")
                        await botLogic.OnGetMemeAsync(client, message.Chat.Id, ct);
                    else if (text == "📊 Статистика")
                        await botLogic.OnStatsAsync(client, message.Chat.Id, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в боте");
            }
        }
        
        private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, HandleErrorSource source, CancellationToken ct)
        {
            _logger.LogError(ex, "Ошибка Telegram Bot API");
            return Task.CompletedTask;
        }
    }
}