using System.ComponentModel.DataAnnotations;

namespace MemeGodBot.ConsoleApp.Models.Entities
{
    public class UserMemeReaction
    {
        [Key]
        public int Id { get; set; }

        public long UserId { get; set; }

        public long QdrantMemeId { get; set; }

        public bool IsLiked { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}