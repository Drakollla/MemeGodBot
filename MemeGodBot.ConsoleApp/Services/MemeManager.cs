using GTranslate.Translators;
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
        private readonly ITextEncoder _textEncoder;
        private readonly ILogger<MemeManager> _logger;
        private readonly QdrantSettings _qdrantSettings;
        private readonly string _baseDownloadPath;
        private readonly RecommendationSettings _recSettings;

        private const float DuplicateThreshold = 0.98f;
        private readonly GoogleTranslator _translator = new GoogleTranslator();

        public MemeManager(MemeDbContext db,
                           QdrantClient qdrant,
                           ILogger<MemeManager> logger,
                           IImageEncoder encoder,
                           ITextEncoder textEncoder,
                           IOptions<QdrantSettings> qdrantOptions,
                           IOptions<RecommendationSettings> recOptions,
                           IOptions<StorageSettings> storageOptions)
        {
            _db = db;
            _qdrant = qdrant;
            _encoder = encoder;
            _textEncoder = textEncoder;
            _qdrantSettings = qdrantOptions.Value;
            _logger = logger;
            _recSettings = recOptions.Value;
            _baseDownloadPath = Path.GetFullPath(storageOptions.Value.MemesFolder);

            if (!Directory.Exists(_baseDownloadPath))
                Directory.CreateDirectory(_baseDownloadPath);
        }

        public async Task ProcessIncomingMemeAsync(IncomingMeme meme, CancellationToken ct)
        {
            if (await _db.Reactions.AnyAsync(r => r.QdrantMemeId == (long)meme.SourceId.GetHashCode(), ct))
                return;

            var localPath = await DownloadMemeAsync(meme);

            if (localPath == null)
                return;

            try
            {
                var vector = _encoder.GenerateEmbedding(localPath);

                if (await IsVisualDuplicateAsync(vector))
                {
                    File.Delete(localPath);
                    return;
                }

                await IndexMemeInQdrantAsync(meme, vector, localPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing meme {Id}", meme.SourceId);

                if (File.Exists(localPath))
                    File.Delete(localPath);
            }
        }

        private async Task<bool> IsVisualDuplicateAsync(float[] vector)
        {
            var duplicates = await _qdrant.SearchAsync(_qdrantSettings.CollectionName, vector, limit: 1);

            if (duplicates.Any() && duplicates[0].Score > DuplicateThreshold)
                return true;

            return false;
        }

        private async Task IndexMemeInQdrantAsync(IncomingMeme meme, float[] vector, string localPath)
        {
            ulong qdrantId = (ulong)meme.SourceId.GetHashCode();

            var point = new PointStruct
            {
                Id = qdrantId,
                Vectors = vector,
                Payload = {
                    ["path"] = localPath,
                    ["source_type"] = meme.SourceType.ToString(),
                    ["channel_id"] = meme.ChannelId ?? "unknown"
                }
            };

            await _qdrant.UpsertAsync(_qdrantSettings.CollectionName, new[] { point });
        }

        public async Task<(ulong Id, string Path)> GetRecommendationAsync(long userId)
        {
            var (likes, dislikes, seenIds) = await GetUserPreferencesAsync(userId);
            var filter = CreateSeenFilter(seenIds);

            if (ShouldUseRandomStrategy(userId, likes.Count))
                return await GetRandomMeme(filter);

            var recommendation = await GetVectorRecommendationAsync(likes, dislikes, filter, userId);

            return recommendation ?? await GetRandomMeme(filter);
        }

        private async Task<(List<PointId> Likes, List<PointId> Dislikes, List<PointId> Seen)> GetUserPreferencesAsync(long userId)
        {
            var reactions = await _db.Reactions
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .Select(r => new { r.IsLiked, r.QdrantMemeId })
                .ToListAsync();

            var likes = new List<PointId>();
            var dislikes = new List<PointId>();
            var seen = new List<PointId>();

            foreach (var r in reactions)
            {
                var pointId = (PointId)(ulong)r.QdrantMemeId;
                seen.Add(pointId);

                if (r.IsLiked)
                    likes.Add(pointId);
                else
                    dislikes.Add(pointId);
            }

            return (likes, dislikes, seen);
        }

        private Filter? CreateSeenFilter(List<PointId> seenIds)
        {
            if (!seenIds.Any())
                return null;

            var filter = new Filter();
            var hasIdCondition = new HasIdCondition();
            hasIdCondition.HasId.AddRange(seenIds);

            filter.MustNot.Add(new Condition { HasId = hasIdCondition });
            return filter;
        }

        private bool ShouldUseRandomStrategy(long userId, int likesCount)
        {
            if (likesCount < _recSettings.MinLikesToStart)
                return true;

            if (Random.Shared.Next(1, 101) <= _recSettings.RandomFactorPercent)
                return true;

            return false;
        }

        private async Task<(ulong Id, string Path)?> GetVectorRecommendationAsync(
            List<PointId> likes,
            List<PointId> dislikes,
            Filter? filter,
            long userId)
        {
            var recs = await _qdrant.RecommendAsync(
                _qdrantSettings.CollectionName,
                positive: likes,
                negative: dislikes,
                filter: filter,
                limit: 1
            );

            if (recs.Any())
            {
                var result = recs.First();
                return (result.Id.Num, result.Payload["path"].StringValue);
            }

            return null;
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
                _logger.LogError(ex, "Error downloading meme {SourceId}", meme.SourceId);
                return null;
            }
        }

        private async Task<(ulong Id, string Path)> GetRandomMeme(Filter? filter)
        {
            var scroll = await _qdrant.ScrollAsync(_qdrantSettings.CollectionName, limit: 50, filter: filter);

            if (scroll.Result == null || !scroll.Result.Any())
                return (0, "");

            var randomMeme = scroll.Result.OrderBy(_ => Guid.NewGuid()).First();

            return (randomMeme.Id.Num, randomMeme.Payload["path"].StringValue);
        }

        public async Task<List<string>> SearchMemesByTextAsync(string text, int limit = 1)
        {
            string englishQuery = text;

            try
            {
                var result = await _translator.TranslateAsync(text, "en");
                englishQuery = result.Translation;
                var vector = _textEncoder.GetTextEmbedding(englishQuery);
                var results = await _qdrant.SearchAsync(
                    collectionName: _qdrantSettings.CollectionName,
                    vector: vector,
                    limit: (ulong)limit);

                var goodResults = results.Where(r => r.Score > 0.19).ToList();

                if (!goodResults.Any())
                    return new List<string>();

                var random = Random.Shared;

                var paths = new List<string>();

                foreach (var point in results)
                {
                    if (point.Payload.TryGetValue("path", out var pathVal))
                    {
                        var path = pathVal.StringValue;

                        if (File.Exists(path))
                            paths.Add(path);
                    }
                }

                return paths;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching meme by text: {Text}", text);
                return new List<string>();
            }
        }
    }
}