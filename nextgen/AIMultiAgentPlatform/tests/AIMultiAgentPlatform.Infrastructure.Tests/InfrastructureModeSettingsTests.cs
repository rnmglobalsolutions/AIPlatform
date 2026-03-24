using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Tests;

public sealed class InfrastructureModeSettingsTests
{
    [Fact]
    public void Resolve_WhenUnset_DefaultsToLeanMode()
    {
        var settings = InfrastructureModeSettings.Resolve(null, null, null, null);

        Assert.Equal(PlatformMode.Lean, settings.PlatformMode);
        Assert.Equal(PersistenceMode.InMemory, settings.PersistenceMode);
        Assert.Equal(MessagingMode.Queue, settings.MessagingMode);
        Assert.Equal(HostingMode.CurrentRuntime, settings.HostingMode);
    }

    [Fact]
    public void Resolve_WhenProductionSelected_DefaultsToProductionModes()
    {
        var settings = InfrastructureModeSettings.Resolve("Production", null, null, null);

        Assert.Equal(PlatformMode.Production, settings.PlatformMode);
        Assert.Equal(PersistenceMode.InMemory, settings.PersistenceMode);
        Assert.Equal(MessagingMode.ServiceBus, settings.MessagingMode);
        Assert.Equal(HostingMode.Dedicated, settings.HostingMode);
    }
}
