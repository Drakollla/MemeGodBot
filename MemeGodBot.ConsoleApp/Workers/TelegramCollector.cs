using MemeGodBot.ConsoleApp.Models.DTOs;
using MemeGodBot.ConsoleApp.Models.Enums;
using MemeGodBot.ConsoleApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using TL;
using WTelegram;
using Channel = System.Threading.Channels.Channel;

namespace MemeGodBot.ConsoleApp.Workers
{
    public class TelegramCollector : BackgroundService
    {
        private readonly ILogger<TelegramCollector> _logger;
        private readonly IConfiguration _config;
        private readonly IServiceScopeFactory _scopeFactory;
        private Client? _client;
        private readonly Channel<UpdatesBase> _updateChannel = Channel.CreateUnbounded<UpdatesBase>();

        public TelegramCollector(ILogger<TelegramCollector> logger,
                               IConfiguration config,
                               IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _config = config;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Запуск Парсера (TelegramWorker)...");

            _client = new Client(what =>
            {
                switch (what)
                {
                    case "api_id": return _config["Telegram:ApiId"];
                    case "api_hash": return _config["Telegram:ApiHash"];
                    case "phone_number": return _config["Telegram:PhoneNumber"];
                    case "verification_code":
                        Console.Write("Введите код для Парсера: ");
                        return Console.ReadLine();
                    case "password":
                        Console.Write("Введите пароль 2FA: ");
                        return Console.ReadLine();
                    default: return null;
                }
            });

            try
            {
                var user = await _client.LoginUserIfNeeded();
                _logger.LogInformation("Парсер залогинен как: {Name}", user.username ?? user.first_name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка входа в WTelegram");
                return;
            }

            _client.OnUpdates += async (u) => await _updateChannel.Writer.WriteAsync(u, stoppingToken);

            await foreach (var update in _updateChannel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await HandleParserUpdate(update, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка обработки обновления парсера");
                }
            }
        }

        private async Task HandleParserUpdate(UpdatesBase updates, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var memeService = scope.ServiceProvider.GetRequiredService<MemeManager>();

            foreach (var update in updates.UpdateList)
            {
                Message? message = update switch
                {
                    UpdateNewMessage unm => unm.message as Message,
                    _ => null
                };

                if (message?.media is MessageMediaPhoto { photo: Photo photo })
                {
                    try
                    {
                        var income = new IncomingMeme
                        {
                            SourceId = photo.id.ToString(),
                            SourceType = MemeSource.Telegram,
                            ChannelId = message.Peer.ID.ToString(),
                            DownloadAction = async (stream) => await _client!.DownloadFileAsync(photo, stream)
                        };

                        await memeService.ProcessIncomingMemeAsync(income, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при индексации мема {Id}", photo.id);
                    }
                }
            }
        }
    }
}