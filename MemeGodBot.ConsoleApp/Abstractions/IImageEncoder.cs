namespace MemeGodBot.ConsoleApp.Abstractions
{
    public interface IImageEncoder
    {
        float[] GenerateEmbedding(string imagePath);
    }
}