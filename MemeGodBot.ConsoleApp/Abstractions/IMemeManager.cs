using MemeGodBot.ConsoleApp.Models.DTOs;

namespace MemeGodBot.ConsoleApp.Abstractions
{
    public interface IMemeManager
    {
        Task ProcessIncomingMemeAsync(IncomingMeme meme, CancellationToken ct);
        Task<(ulong Id, string Path)> GetRecommendationAsync(long userId);
    }
}