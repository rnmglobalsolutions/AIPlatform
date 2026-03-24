using AIMultiAgentPlatform.Application.DependencyInjection;
using AIMultiAgentPlatform.Infrastructure.DependencyInjection;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration["PlatformMode"],
    builder.Configuration["Infrastructure:PersistenceMode"],
    builder.Configuration["Infrastructure:MessagingMode"],
    builder.Configuration["Infrastructure:HostingMode"]);

builder.Build().Run();
