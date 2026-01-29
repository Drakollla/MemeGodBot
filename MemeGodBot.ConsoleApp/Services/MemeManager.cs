using MemeGodBot.ConsoleApp.Abstractions;
using MemeGodBot.ConsoleApp.Configurations;
using MemeGodBot.ConsoleApp.Models.Context;
using MemeGodBot.ConsoleApp.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace MemeGodBot.ConsoleApp.Services
{
    public class MemeManager : IMemeManager
    {
        private readonly MemeDbContext _db;      
        private readonly QdrantClient _qdrant;   
        private readonly IImageEncoder _encoder;
        private readonly ILogger<MemeManager> _logger;
        private readonly QdrantSettings _settings;
        private readonly string _baseDownloadPath;

        public MemeManager(MemeDbContext db,
                           QdrantClient qdrant,
                           ILogger<MemeManager> logger,
                           IImageEncoder encoder,
                           IOptions<QdrantSettings> qdrantOptions,
                           IOptions<StorageSettings> storageOptions)
        {
            _db = db;
            _qdrant = qdrant;
            _encoder = encoder;
            _settings = qdrantOptions.Value;
            _logger = logger;

            _baseDownloadPath = Path.GetFullPath(storageOptions.Value.MemesFolder);

            if (!Directory.Exists(_baseDownloadPath))
                Directory.CreateDirectory(_baseDownloadPath);
        }

        public async Task ProcessIncomingMemeAsync(IncomingMeme meme, CancellationToken ct)
        {
            if (await _db.Reactions.AnyAsync(r => r.QdrantMemeId == (long)meme.SourceId.GetHashCode(), ct))
            {
                _logger.LogInformation("Мем с SourceId {Id} уже обрабатывался.", meme.SourceId);
                return;
            }

            var localPath = await DownloadMemeAsync(meme);
            
            if (localPath == null) 
                return;

            try
            {
                var vector = _encoder.GenerateEmbedding(localPath);
                var duplicates = await _qdrant.SearchAsync("memes", vector, limit: 1);
                
                if (duplicates.Any() && duplicates[0].Score > 0.98f)
                {
                    _logger.LogInformation("Обнаружен визуальный дубликат ({Score}). Удаляю файл.", duplicates[0].Score);
                    File.Delete(localPath);
                    return;
                }

                ulong qdrantId = (ulong)meme.SourceId.GetHashCode();
                
                await _qdrant.UpsertAsync(_settings.CollectionName, new[]
                {
                    new PointStruct
                    {
                        Id = qdrantId,
                        Vectors = vector,
                        Payload = {
                            ["path"] = localPath,
                            ["source_type"] = meme.SourceType.ToString(),
                            ["channel_id"] = meme.ChannelId ?? "unknown"
                        }
                    }
                });

                _logger.LogInformation("Мем успешно проиндексирован. ID: {Id}", qdrantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке мема {Id}", meme.SourceId);
            }
        }

        public async Task<(ulong Id, string Path)> GetRecommendationAsync(long userId)
        {
            var reactions = await _db.Reactions
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .ToListAsync();

            var likes = reactions.Where(r => r.IsLiked).Select(r => (PointId)(ulong)r.QdrantMemeId).ToList();
            var dislikes = reactions.Where(r => !r.IsLiked).Select(r => (PointId)(ulong)r.QdrantMemeId).ToList();
            var seenPointIds = reactions.Select(r => (PointId)(ulong)r.QdrantMemeId).ToList();

            Filter? filter = null;
            if (seenPointIds.Any())
            {
                filter = new Filter();
                var hasIdCondition = new HasIdCondition();
                hasIdCondition.HasId.AddRange(seenPointIds);

                filter.MustNot.Add(new Condition { HasId = hasIdCondition });
            }

            if (likes.Count < 3)
            {
                var scrollResponse = await _qdrant.ScrollAsync(_settings.CollectionName, limit: 20, filter: filter);

                if (scrollResponse.Result == null || !scrollResponse.Result.Any())
                    throw new Exception("База мемов пуста или все мемы уже просмотрены!");

                var randomMeme = scrollResponse.Result.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
                
                if (randomMeme == null)
                    return (0, string.Empty);

                return (randomMeme.Id.Num, randomMeme.Payload["path"].StringValue);
            }

            var recs = await _qdrant.RecommendAsync(_settings.CollectionName,
                positive: likes,
                negative: dislikes,
                filter: filter,
                limit: 1
            );

            if (recs != null && recs.Any())
            {
                var result = recs.First();
                return (result.Id.Num, result.Payload["path"].StringValue);
            }

            var fallback = await _qdrant.ScrollAsync(_settings.CollectionName, limit: 1, filter: filter);
            var fallbackMeme = fallback.Result?.FirstOrDefault();

            if (fallbackMeme == null) 
                return (0, string.Empty);

            return (fallbackMeme.Id.Num, fallbackMeme.Payload["path"].StringValue);
        }

        private async Task<string?> DownloadMemeAsync(IncomingMeme meme)
        {
            try
            {
                var dateFolder = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var targetFolder = Path.Combine(_baseDownloadPath, dateFolder);
                Directory.CreateDirectory(targetFolder);

                var fileName = $"{Guid.NewGuid()}{meme.FileExtension}";
                var fullPath = Path.Combine(targetFolder, fileName);

                using var fs = File.Create(fullPath);
                await meme.DownloadAction(fs);

                return fullPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка скачивания мема {SourceId}", meme.SourceId);
                return null;
            }
        }
    }
}