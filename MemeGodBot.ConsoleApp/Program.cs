using MemeGodBot.ConsoleApp;
using MemeGodBot.ConsoleApp.Models.Context;
using MemeGodBot.ConsoleApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qdrant.Client;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var clipPath = builder.Configuration["Models:ClipPath"];

builder.Services.AddDbContext<MemeDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddSingleton(sp =>
    new QdrantClient(builder.Configuration["Qdrant:Host"],
                     int.Parse(builder.Configuration["Qdrant:Port"])));

builder.Services.AddSingleton(sp => new ClipEmbedder(clipPath));
builder.Services.AddScoped<MemeService>();

using IHost host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<MemeDbContext>();
    var embedder = services.GetRequiredService<ClipEmbedder>();
}

await host.RunAsync();