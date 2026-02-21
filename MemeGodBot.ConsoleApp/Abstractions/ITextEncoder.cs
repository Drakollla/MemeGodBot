namespace MemeGodBot.ConsoleApp.Abstractions
{
    public interface ITextEncoder
    {
        float[] GetTextEmbedding(string text);
    }
}