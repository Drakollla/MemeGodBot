using MemeGodBot.ConsoleApp.Extensions;
using MemeGodBot.ConsoleApp.Models.Context;
using MemeGodBot.ConsoleApp.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMemeInfrastructure(builder.Configuration);

builder.Services.AddDbContext<MemeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHostedService<TelegramCollector>();
builder.Services.AddHostedService<TelegramBotListener>();

using IHost host = builder.Build();

await host.BootstrapDatabaseAsync();

await host.RunAsync();