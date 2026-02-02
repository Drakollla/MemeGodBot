using MemeGodBot.ConsoleApp.Abstractions;
using MemeGodBot.ConsoleApp.Configurations;
using MemeGodBot.ConsoleApp.Models.DTOs;
using MemeGodBot.ConsoleApp.Models.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;
using TL;
using WTelegram;
using Channel = System.Threading.Channels.Channel;

namespace MemeGodBot.ConsoleApp.Workers;

public class TelegramCollector : BackgroundService
{
    private readonly ILogger<TelegramCollector> _logger;
    private readonly TelegramSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private Client? _client;
    private readonly Channel<UpdatesBase> _updateChannel = Channel.CreateUnbounded<UpdatesBase>();

    public TelegramCollector(ILogger<TelegramCollector> logger,
                             IOptions<TelegramSettings> tgOptions,
                             IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _settings = tgOptions.Value;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Запуск Telegram Collector...");

        _client = new Client(what => what switch
        {
            "api_id" => _settings.ApiId.ToString(),
            "api_hash" => _settings.ApiHash,
            "phone_number" => _settings.PhoneNumber,
            "verification_code" => ReadInput("Введите код для Telegram: "),
            "password" => ReadInput("Введите пароль 2FA: "),
            _ => null
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
                _logger.LogError(ex, "Ошибка при обработке обновления");
            }
        }
    }

    private async Task HandleParserUpdate(UpdatesBase updates, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var memeManager = scope.ServiceProvider.GetRequiredService<IMemeManager>();

        foreach (var update in updates.UpdateList)
        {
            if (update is UpdateNewMessage unm && unm.message is Message message)
            {
                var currentPeerId = message.Peer.ID.ToString();
                
                if (_settings.TargetChannels.Any() && !_settings.TargetChannels.Contains(currentPeerId))
                    continue;

                if (message.media is MessageMediaPhoto { photo: Photo photo })
                {
                    _logger.LogInformation("Обнаружен мем {Id} в канале {Peer}", photo.id, message.Peer.ID);

                    var candidate = new IncomingMeme
                    {
                        SourceId = photo.id.ToString(),
                        SourceType = MemeSource.Telegram,
                        ChannelId = message.Peer.ID.ToString(),
                        FileExtension = ".jpg",
                        DownloadAction = async (stream) => await _client!.DownloadFileAsync(photo, stream)
                    };

                    await memeManager.ProcessIncomingMemeAsync(candidate, ct);
                }
            }
        }
    }

    private string? ReadInput(string prompt)
    {
        Console.Write(prompt);

        return Console.ReadLine();
    }
}