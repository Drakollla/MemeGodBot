using MemeGodBot.ConsoleApp.Abstractions;
using MemeGodBot.ConsoleApp.Configurations;
using MemeGodBot.ConsoleApp.Models.DTOs;
using MemeGodBot.ConsoleApp.Models.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;

namespace MemeGodBot.ConsoleApp.Workers
{
    public class RedditCollector : BackgroundService
    {
        private readonly ILogger<RedditCollector> _logger;
        private readonly RedditSettings _settings;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;

        public RedditCollector(ILogger<RedditCollector> logger,
                               IOptions<RedditSettings> options,
                               IServiceScopeFactory scopeFactory,
                               IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _settings = options.Value;
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Запуск Reddit Collector (RSS)...");

            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var sub in _settings.TargetSubreddits)
                {
                    if (stoppingToken.IsCancellationRequested) 
                        break;
                    
                    await ProcessSubredditAsync(sub, stoppingToken);
                }

                _logger.LogInformation("Reddit-парсинг завершен. Сон на {N} минут...", _settings.RefreshIntervalMinutes);
                
                await Task.Delay(TimeSpan.FromMinutes(_settings.RefreshIntervalMinutes), stoppingToken);
            }
        }

        private async Task ProcessSubredditAsync(string subName, CancellationToken ct)
        {
            try
            {
                var url = $"https://www.reddit.com/r/{subName}/hot/.rss?limit=20";

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd(_settings.UserAgent);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

                using var stream = await client.GetStreamAsync(url, ct);
                using var xmlReader = XmlReader.Create(stream);
                var feed = SyndicationFeed.Load(xmlReader);

                if (feed == null) 
                    return;

                using var scope = _scopeFactory.CreateScope();
                var memeManager = scope.ServiceProvider.GetRequiredService<IMemeManager>();

                foreach (var item in feed.Items)
                {
                    var imageUrl = ExtractImageUrl(item);
                    
                    if (string.IsNullOrEmpty(imageUrl))
                        continue;

                    var candidate = new IncomingMeme
                    {
                        SourceId = item.Id,
                        SourceType = MemeSource.Reddit,
                        ChannelId = subName,
                        FileExtension = GetExtensionSafe(imageUrl),

                        DownloadAction = async (fileStream) =>
                        {
                            var bytes = await client.GetByteArrayAsync(imageUrl, ct);
                            await fileStream.WriteAsync(bytes, ct);
                        }
                    };

                    await memeManager.ProcessIncomingMemeAsync(candidate, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка r/{Sub}: {Msg}", subName, ex.Message);
            }
        }

        private string? ExtractImageUrl(SyndicationItem item)
        {
            var content = (item.Content as TextSyndicationContent)?.Text
                          ?? item.Summary?.Text
                          ?? "";

            var match = Regex.Match(content, @"href=""(https://i\.redd\.it/[^""]+\.(jpg|png|jpeg))""", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;

            return item.Links.FirstOrDefault(l =>
                l.Uri.AbsoluteUri.Contains("i.redd.it") &&
                (l.Uri.AbsoluteUri.EndsWith(".jpg") || l.Uri.AbsoluteUri.EndsWith(".png")))?.Uri.AbsoluteUri;
        }

        private string GetExtensionSafe(string url)
        {
            var ext = Path.GetExtension(url).Split('?')[0];
            return string.IsNullOrEmpty(ext) ? ".jpg" : ext;
        }
    }
}
