using MemeGodBot.ConsoleApp.Models.Context;
using MemeGodBot.ConsoleApp.Services;
using MemeGodBot.ConsoleApp.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qdrant.Client;
using Qdrant.Client.Grpc;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var clipPath = builder.Configuration["Models:ClipPath"];

builder.Services.AddSingleton(sp => new ImageEncoder(clipPath));

builder.Services.AddSingleton(sp =>
    new QdrantClient(builder.Configuration["Qdrant:Host"],
                     int.Parse(builder.Configuration["Qdrant:Port"])));

builder.Services.AddDbContext<MemeDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<MemeManager>();
builder.Services.AddHostedService<TelegramCollector>();
builder.Services.AddScoped<MemeBotUiService>();
builder.Services.AddHostedService<TelegramBotListener>();

using IHost host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var qdrantClient = scope.ServiceProvider.GetRequiredService<QdrantClient>();
    var collections = await qdrantClient.ListCollectionsAsync();

    if (!collections.Contains("memes"))
    {
        await qdrantClient.CreateCollectionAsync("memes",
            new VectorParams
            {
                Size = 512,
                Distance = Distance.Cosine
            });
    }
}

await host.RunAsync();