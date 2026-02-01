using MemeGodBot.ConsoleApp.Extensions;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMemeInfrastructure(builder.Configuration);

using IHost host = builder.Build();

await host.BootstrapDatabaseAsync();
await host.RunAsync();