using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Domain.Tests;

public sealed class TenantTests
{
    [Fact]
    public void Create_NormalizesProfileAndSlug()
    {
        var profile = new ClientProfile(
            "  RNM Labs  ",
            "  Jane Doe ",
            " JANE@EXAMPLE.COM ",
            " Coaches ",
            " Growth Program ",
            " Founders ",
            " Bold ",
            " BOOK ",
            ["Instagram", "Instagram", "LinkedIn"],
            ["Low visibility"],
            ["Too expensive"],
            []);

        var tenant = Tenant.Create(new TenantId("tenant_123"), "RNM-LABS", profile, new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal("rnm-labs", tenant.Slug);
        Assert.Equal("RNM Labs", tenant.DisplayName);
        Assert.Equal("jane@example.com", tenant.Profile.PrimaryContactEmail);
        Assert.Equal(2, tenant.Profile.Platforms.Count);
    }

    [Fact]
    public void Create_ThrowsWhenSlugMissing()
    {
        var profile = new ClientProfile(
            "RNM",
            "Jane",
            "jane@example.com",
            "Coaches",
            "Growth",
            "Founders",
            "Bold",
            "BOOK",
            ["Instagram"],
            ["Low visibility"],
            ["Too expensive"],
            []);

        Assert.Throws<ArgumentException>(() => Tenant.Create(new TenantId("tenant_123"), " ", profile, DateTime.UtcNow));
    }
}
