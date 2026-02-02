using MemeGodBot.ConsoleApp.Configurations;
using MemeGodBot.ConsoleApp.Helpers;
using MemeGodBot.ConsoleApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MemeGodBot.ConsoleApp.Workers
{
    public class TelegramBotListener : BackgroundService
    {
        private readonly BotSettings _settings;
        private readonly ILogger<TelegramBotListener> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private TelegramBotClient? _botClient;

        public TelegramBotListener(IOptions<BotSettings> botOptions,
                                   ILogger<TelegramBotListener> logger,
                                   IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _settings = botOptions.Value;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(_settings.Token))
                return;

            _botClient = new TelegramBotClient(_settings.Token);

            _botClient.StartReceiving(
                updateHandler: HandleBotUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: new ReceiverOptions { AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery] },
                cancellationToken: stoppingToken);

            var me = await _botClient.GetMe(stoppingToken);
            _logger.LogInformation("Bot interface launched: {BotName}", me.FirstName);

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
                    if (text.StartsWith(BotConstants.Commands.Start))
                        await botLogic.OnStartAsync(client, message, ct);
                    else if (text == BotConstants.Buttons.GetMeme)
                        await botLogic.OnGetMemeAsync(client, message.Chat.Id, ct);
                    else if (text == BotConstants.Buttons.Stats)
                        await botLogic.OnStatsAsync(client, message.Chat.Id, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bot");
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, HandleErrorSource source, CancellationToken ct)
        {
            _logger.LogError(ex, "Telegram Bot API Error");
            return Task.CompletedTask;
        }
    }
}