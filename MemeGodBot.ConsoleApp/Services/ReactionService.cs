using MemeGodBot.ConsoleApp.Abstractions;
using MemeGodBot.ConsoleApp.Models.Context;
using MemeGodBot.ConsoleApp.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MemeGodBot.ConsoleApp.Services
{
    public class ReactionService : IReactionService
    {
        private readonly MemeDbContext _db;

        public ReactionService(MemeDbContext db)
        {
            _db = db;
        }

        public async Task AddReactionAsync(long userId, ulong memeId, bool isLiked, CancellationToken ct)
        {
            var qdrantId = (long)memeId;
            var existing = await _db.Reactions.FirstOrDefaultAsync(r => r.UserId == userId && r.QdrantMemeId == qdrantId, ct);

            if (existing != null)
            {
                existing.IsLiked = isLiked;
            }
            else
            {
                _db.Reactions.Add(new UserMemeReaction
                {
                    UserId = userId,
                    QdrantMemeId = qdrantId,
                    IsLiked = isLiked
                });
            }

            await _db.SaveChangesAsync(ct);
        }

        public async Task<(int Likes, int Dislikes)> GetUserStatsAsync(long userId, CancellationToken ct)
        {
            var likes = await _db.Reactions.CountAsync(r => r.UserId == userId && r.IsLiked, ct);
            var dislikes = await _db.Reactions.CountAsync(r => r.UserId == userId && !r.IsLiked, ct);

            return (likes, dislikes);
        }
    }
}