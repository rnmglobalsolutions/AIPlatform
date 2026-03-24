using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Tenants;

public sealed record Tenant(
    TenantId TenantId,
    string Slug,
    string DisplayName,
    ClientProfile Profile,
    DateTime CreatedUtc)
{
    public static Tenant Create(TenantId tenantId, string slug, ClientProfile profile, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Slug is required.", nameof(slug));
        }

        var normalizedProfile = profile.Normalize();
        return new Tenant(
            tenantId,
            slug.Trim().ToLowerInvariant(),
            normalizedProfile.BusinessName,
            normalizedProfile,
            utcNow);
    }
}
