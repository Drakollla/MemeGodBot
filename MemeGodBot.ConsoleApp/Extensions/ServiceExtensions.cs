using MemeGodBot.ConsoleApp.Abstractions;
using MemeGodBot.ConsoleApp.Configurations;
using MemeGodBot.ConsoleApp.Services;
using MemeGodBot.ConsoleApp.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qdrant.Client;

namespace MemeGodBot.ConsoleApp.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddMemeInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<TelegramSettings>(config.GetSection("Telegram"));
            services.Configure<QdrantSettings>(config.GetSection("Qdrant"));
            services.Configure<BotSettings>(config.GetSection("Bot"));
            services.Configure<ModelSettings>(config.GetSection("Models"));
            services.Configure<StorageSettings>(config.GetSection("Storage"));
            services.Configure<RecommendationSettings>(config.GetSection("Recommendation"));

            services.AddSingleton<IImageEncoder>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<ModelSettings>>().Value;
                return new ImageEncoder(settings.ClipPath);
            });

            services.AddSingleton(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<QdrantSettings>>().Value;
                return new QdrantClient(settings.Host, settings.Port);
            });

            services.AddScoped<IMemeManager, MemeManager>();
            services.AddScoped<MemeBotUiService>();

            services.Configure<RedditSettings>(config.GetSection("Reddit"));
            services.AddHttpClient();
            services.AddHostedService<RedditCollector>();

            return services;
        }
    }
}