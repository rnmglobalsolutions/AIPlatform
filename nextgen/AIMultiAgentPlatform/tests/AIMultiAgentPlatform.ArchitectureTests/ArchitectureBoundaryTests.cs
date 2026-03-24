namespace AIMultiAgentPlatform.ArchitectureTests;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void Domain_ShouldNotReferenceInfrastructureAssembly()
    {
        var domainReferences = typeof(AIMultiAgentPlatform.Domain.Tenants.Tenant).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("AIMultiAgentPlatform.Infrastructure", domainReferences);
    }

    [Fact]
    public void Application_ShouldNotReferenceApiAssembly()
    {
        var applicationReferences = typeof(AIMultiAgentPlatform.Application.Intake.ProcessTallySubmissionUseCase).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("AIMultiAgentPlatform.Api", applicationReferences);
    }
}
