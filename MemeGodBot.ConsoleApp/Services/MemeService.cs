using MemeGodBot.ConsoleApp.DTOs;
using MemeGodBot.ConsoleApp.Models.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace MemeGodBot.ConsoleApp.Services
{
    public class MemeService
    {
        private readonly MemeDbContext _db;      
        private readonly QdrantClient _qdrant;   
        private readonly ClipEmbedder _embedder;
        private readonly ILogger<MemeService> _logger;
        private readonly string _baseDownloadPath;

        public MemeService(MemeDbContext db,
                           QdrantClient qdrant,
                           ClipEmbedder embedder,
                           ILogger<MemeService> logger)
        {
            _db = db;
            _qdrant = qdrant;
            _embedder = embedder;
            _logger = logger;
            _baseDownloadPath = Path.Combine(Directory.GetCurrentDirectory(), "Downloads");
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
                var vector = _embedder.GenerateEmbedding(localPath);
                var duplicates = await _qdrant.SearchAsync("memes", vector, limit: 1);
                
                if (duplicates.Any() && duplicates[0].Score > 0.98f)
                {
                    _logger.LogInformation("Обнаружен визуальный дубликат ({Score}). Удаляю файл.", duplicates[0].Score);
                    File.Delete(localPath);
                    return;
                }

                ulong qdrantId = (ulong)meme.SourceId.GetHashCode();
                
                await _qdrant.UpsertAsync("memes", new[]
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