using AIMultiAgentPlatform.Application.DependencyInjection;
using AIMultiAgentPlatform.Infrastructure.DependencyInjection;
using AIMultiAgentPlatform.Workers.Observability;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.UseMiddleware<CorrelationFunctionMiddleware>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Build().Run();
