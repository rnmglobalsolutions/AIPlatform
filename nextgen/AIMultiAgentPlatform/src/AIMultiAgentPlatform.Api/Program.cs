using AIMultiAgentPlatform.Application.DependencyInjection;
using AIMultiAgentPlatform.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration["PlatformMode"],
    builder.Configuration["Infrastructure:PersistenceMode"],
    builder.Configuration["Infrastructure:MessagingMode"],
    builder.Configuration["Infrastructure:HostingMode"]);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
