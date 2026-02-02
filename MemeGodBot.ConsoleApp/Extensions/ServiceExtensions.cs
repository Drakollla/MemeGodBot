using MemeGodBot.ConsoleApp.Abstractions;
using MemeGodBot.ConsoleApp.Configurations;
using MemeGodBot.ConsoleApp.Models.Context;
using MemeGodBot.ConsoleApp.Services;
using MemeGodBot.ConsoleApp.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using System.Net.Http.Headers;

namespace MemeGodBot.ConsoleApp.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddMemeInfrastructure(this IServiceCollection services, IConfiguration config) =>
            services.AddBotConfigurations(config)
                .AddDatabase(config)
                .AddExternalClients()
                .AddInternalServices()
                .AddBackgroundWorkers();

        private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
        {
            services.AddDbContext<MemeDbContext>(options =>
                options.UseSqlServer(config.GetConnectionString("DefaultConnection")));

            return services;
        }

        private static IServiceCollection AddBotConfigurations(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<TelegramSettings>(config.GetSection("Telegram"));
            services.Configure<QdrantSettings>(config.GetSection("Qdrant"));
            services.Configure<BotSettings>(config.GetSection("Bot"));
            services.Configure<ModelSettings>(config.GetSection("Models"));
            services.Configure<StorageSettings>(config.GetSection("Storage"));
            services.Configure<RecommendationSettings>(config.GetSection("Recommendation"));
            services.Configure<RedditSettings>(config.GetSection("Reddit"));

            return services;
        }

        private static IServiceCollection AddExternalClients(this IServiceCollection services)
        {
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

            services.AddHttpClient("RedditClient", (sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptions<RedditSettings>>().Value;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
                client.Timeout = TimeSpan.FromSeconds(30);
            }).AddStandardResilienceHandler();

            return services;
        }

        private static IServiceCollection AddInternalServices(this IServiceCollection services)
        {
            services.AddScoped<IMemeManager, MemeManager>();
            services.AddScoped<IReactionService, ReactionService>();

            services.AddScoped<MemeBotUiService>();

            return services;
        }

        private static IServiceCollection AddBackgroundWorkers(this IServiceCollection services)
        {
            services.AddHostedService<RedditCollector>();
            services.AddHostedService<TelegramCollector>();
            services.AddHostedService<TelegramBotListener>();
            return services;
        }
    }
}