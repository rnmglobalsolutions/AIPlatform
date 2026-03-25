using AIMultiAgentPlatform.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AIMultiAgentPlatform.Infrastructure.Tests;

public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructure_WhenLeanTableModeIsRequestedWithoutConfiguration_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(
                platformMode: "Lean",
                persistenceMode: "Table",
                messagingMode: "Queue",
                hostingMode: "FunctionsConsumption"));

        Assert.Contains("Lean table persistence requires IConfiguration", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddInfrastructure_WhenProductionSqlModeIsRequested_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<NotSupportedException>(() =>
            services.AddInfrastructure(
                platformMode: "Production",
                persistenceMode: "Sql",
                messagingMode: "ServiceBus",
                hostingMode: "Dedicated"));

        Assert.Contains("SQL persistence wiring has not been implemented yet", exception.Message, StringComparison.Ordinal);
    }
}
