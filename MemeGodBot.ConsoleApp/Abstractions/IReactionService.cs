namespace MemeGodBot.ConsoleApp.Abstractions
{
    public interface IReactionService
    {
        Task AddReactionAsync(long userId, ulong memeId, bool isLiked, CancellationToken ct);
        Task<(int Likes, int Dislikes)> GetUserStatsAsync(long userId, CancellationToken ct);
    }
}