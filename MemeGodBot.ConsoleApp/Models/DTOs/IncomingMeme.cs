using MemeGodBot.ConsoleApp.Models.Enums;

namespace MemeGodBot.ConsoleApp.Models.DTOs
{
    public class IncomingMeme
    {
        public required string SourceId { get; set; }

        public required MemeSource SourceType { get; set; }

        public required string ChannelId { get; set; }

        public string FileExtension { get; set; } = ".jpg";

        public required Func<Stream, Task> DownloadAction { get; set; }
    }
}