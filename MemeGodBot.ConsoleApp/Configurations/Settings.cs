namespace MemeGodBot.ConsoleApp.Configurations
{
    public class TelegramSettings
    {
        public int ApiId { get; set; }
        public string ApiHash { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class QdrantSettings
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 6334;
        public string CollectionName { get; set; } = "memes";
    }

    public class BotSettings
    {
        public string Token { get; set; } = string.Empty;
        public long ArchiveChannelId { get; set; }
    }

    public class ModelSettings
    {
        public string ClipPath { get; set; } = string.Empty;
    }
    public class StorageSettings
    {
        public string MemesFolder { get; set; } = "Downloads";
    }
}