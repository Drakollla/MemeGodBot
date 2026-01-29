using MemeGodBot.ConsoleApp.Models.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace MemeGodBot.ConsoleApp.Extensions
{
    public static class HostExtensions
    {
        public static async Task BootstrapDatabaseAsync(this IHost host)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<Program>>();

            try
            {
                var context = services.GetRequiredService<MemeDbContext>();
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("SQL Server database is ready.");

                var qdrantClient = services.GetRequiredService<QdrantClient>();
                var collections = await qdrantClient.ListCollectionsAsync();

                if (!collections.Contains("memes"))
                {
                    logger.LogInformation("Creating Qdrant collection 'memes'...");
                    await qdrantClient.CreateCollectionAsync("memes",
                        new VectorParams { Size = 512, Distance = Distance.Cosine });
                }
                logger.LogInformation("Qdrant collection is ready.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while bootstrapping the databases.");
                throw;
            }
        }
    }
}