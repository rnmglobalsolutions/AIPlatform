using AIMultiAgentPlatform.Application.DependencyInjection;
using AIMultiAgentPlatform.Functions.Observability;
using AIMultiAgentPlatform.Infrastructure.DependencyInjection;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.UseMiddleware<CorrelationFunctionMiddleware>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Build().Run();
