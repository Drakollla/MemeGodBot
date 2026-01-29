using MemeGodBot.ConsoleApp.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MemeGodBot.ConsoleApp.Models.Context
{
    public class MemeDbContext : DbContext
    {
        public MemeDbContext(DbContextOptions<MemeDbContext> options) : base(options) { }

        public DbSet<UserMemeReaction> Reactions { get; set; }
    }
}
