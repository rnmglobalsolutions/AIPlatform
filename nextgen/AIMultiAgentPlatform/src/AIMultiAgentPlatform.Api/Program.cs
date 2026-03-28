using AIMultiAgentPlatform.Application.DependencyInjection;
using AIMultiAgentPlatform.Api.Observability;
using AIMultiAgentPlatform.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseMiddleware<CorrelationIdMiddleware>();
app.MapControllers();

app.Run();
