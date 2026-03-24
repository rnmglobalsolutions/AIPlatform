using AIMultiAgentPlatform.Application.DependencyInjection;
using AIMultiAgentPlatform.Infrastructure.DependencyInjection;
using AIMultiAgentPlatform.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration["PlatformMode"],
    builder.Configuration["Infrastructure:PersistenceMode"],
    builder.Configuration["Infrastructure:MessagingMode"],
    builder.Configuration["Infrastructure:HostingMode"]);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
