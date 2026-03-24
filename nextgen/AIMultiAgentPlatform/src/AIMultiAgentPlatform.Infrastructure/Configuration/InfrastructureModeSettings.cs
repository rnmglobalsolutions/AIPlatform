namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record InfrastructureModeSettings(
    PlatformMode PlatformMode,
    PersistenceMode PersistenceMode,
    MessagingMode MessagingMode,
    HostingMode HostingMode)
{
    public static InfrastructureModeSettings Resolve(
        string? platformMode,
        string? persistenceMode,
        string? messagingMode,
        string? hostingMode)
    {
        var resolvedPlatformMode = ParsePlatformMode(platformMode);

        var resolvedPersistenceMode = ParsePersistenceMode(persistenceMode, PersistenceMode.InMemory);

        var resolvedMessagingMode = ParseMessagingMode(
            messagingMode,
            resolvedPlatformMode == PlatformMode.Lean ? MessagingMode.Queue : MessagingMode.ServiceBus);

        var resolvedHostingMode = ParseHostingMode(
            hostingMode,
            resolvedPlatformMode == PlatformMode.Lean ? HostingMode.CurrentRuntime : HostingMode.Dedicated);

        return new InfrastructureModeSettings(
            resolvedPlatformMode,
            resolvedPersistenceMode,
            resolvedMessagingMode,
            resolvedHostingMode);
    }

    private static PlatformMode ParsePlatformMode(string? value) =>
        Enum.TryParse<PlatformMode>(value, true, out var parsed) ? parsed : PlatformMode.Lean;

    private static PersistenceMode ParsePersistenceMode(string? value, PersistenceMode fallback) =>
        Enum.TryParse<PersistenceMode>(value, true, out var parsed) ? parsed : fallback;

    private static MessagingMode ParseMessagingMode(string? value, MessagingMode fallback) =>
        Enum.TryParse<MessagingMode>(value, true, out var parsed) ? parsed : fallback;

    private static HostingMode ParseHostingMode(string? value, HostingMode fallback) =>
        Enum.TryParse<HostingMode>(value, true, out var parsed) ? parsed : fallback;
}
