using System.Xml.Linq;

namespace LIAnsureProtect.UnitTests.Architecture;


public sealed class ProjectReferenceBoundaryTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Theory]
    // Platform shared kernel: Ports (Abstractions) depend on NOTHING; adapters (Platform) depend only on Ports.
    [InlineData("src/Platform/LIAnsureProtect.Platform.Abstractions/LIAnsureProtect.Platform.Abstractions.csproj")]
    [InlineData(
        "src/Platform/LIAnsureProtect.Platform/LIAnsureProtect.Platform.csproj",
        "LIAnsureProtect.Platform.Abstractions")]
    // Legacy layered projects (strangled into modules over later milestones).
    [InlineData(
        "src/LIAnsureProtect.Domain/LIAnsureProtect.Domain.csproj",
        "LIAnsureProtect.Platform.Abstractions")]
    [InlineData(
        "src/LIAnsureProtect.Application/LIAnsureProtect.Application.csproj",
        "LIAnsureProtect.Domain",
        "LIAnsureProtect.Modules.Underwriting.Application")]
    [InlineData(
        "src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj",
        "LIAnsureProtect.Application",
        "LIAnsureProtect.Domain",
        "LIAnsureProtect.Modules.Claims.Application",
        "LIAnsureProtect.Modules.Notifications.Application",
        "LIAnsureProtect.Modules.Quoting.Application",
        "LIAnsureProtect.Modules.Underwriting.Application",
        "LIAnsureProtect.Modules.Underwriting.Domain",
        "LIAnsureProtect.Platform.Abstractions")]
    [InlineData(
        "src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj",
        "LIAnsureProtect.Application",
        "LIAnsureProtect.Infrastructure",
        "LIAnsureProtect.Modules.Claims.Infrastructure",
        "LIAnsureProtect.Modules.Notifications.Infrastructure",
        "LIAnsureProtect.Modules.Quoting.Infrastructure",
        "LIAnsureProtect.Modules.Underwriting.Infrastructure",
        "LIAnsureProtect.Platform")]
    [InlineData(
        "src/LIAnsureProtect.Worker/LIAnsureProtect.Worker.csproj",
        "LIAnsureProtect.Application",
        "LIAnsureProtect.Infrastructure",
        "LIAnsureProtect.Modules.Claims.Infrastructure",
        "LIAnsureProtect.Modules.Notifications.Infrastructure",
        "LIAnsureProtect.Modules.Quoting.Infrastructure",
        "LIAnsureProtect.Modules.Underwriting.Infrastructure",
        "LIAnsureProtect.Platform")]
    [InlineData(
        "src/Modules/Claims/LIAnsureProtect.Modules.Claims.Domain/LIAnsureProtect.Modules.Claims.Domain.csproj",
        "LIAnsureProtect.Platform.Abstractions")]
    [InlineData(
        "src/Modules/Claims/LIAnsureProtect.Modules.Claims.Application/LIAnsureProtect.Modules.Claims.Application.csproj",
        "LIAnsureProtect.Modules.Claims.Domain",
        "LIAnsureProtect.Platform.Abstractions")]
    [InlineData(
        "src/Modules/Claims/LIAnsureProtect.Modules.Claims.Infrastructure/LIAnsureProtect.Modules.Claims.Infrastructure.csproj",
        "LIAnsureProtect.Modules.Claims.Application",
        "LIAnsureProtect.Modules.Claims.Domain",
        "LIAnsureProtect.Platform",
        "LIAnsureProtect.Platform.Abstractions")]
    [InlineData(
        "src/Modules/Quoting/LIAnsureProtect.Modules.Quoting.Domain/LIAnsureProtect.Modules.Quoting.Domain.csproj",
        "LIAnsureProtect.Platform.Abstractions")]
    [InlineData(
        "src/Modules/Quoting/LIAnsureProtect.Modules.Quoting.Application/LIAnsureProtect.Modules.Quoting.Application.csproj",
        "LIAnsureProtect.Modules.Quoting.Domain",
        "LIAnsureProtect.Platform.Abstractions")]
    [InlineData(
        "src/Modules/Quoting/LIAnsureProtect.Modules.Quoting.Infrastructure/LIAnsureProtect.Modules.Quoting.Infrastructure.csproj",
        "LIAnsureProtect.Modules.Quoting.Application",
        "LIAnsureProtect.Modules.Quoting.Domain")]
    public void ProjectReferencesFollowCleanArchitectureDirection(
        string projectPath,
        params string[] expectedReferencedProjects)
    {
        // Arrange
        var fullProjectPath = Path.Combine(RepositoryRoot, projectPath);

        // Act
        var actualReferencedProjects = ReadReferencedProjectNames(fullProjectPath);

        // Assert
        Assert.Equal(expectedReferencedProjects.Order().ToArray(), actualReferencedProjects);
    }

    /// <summary>
    /// The module-boundary ratchet. It discovers every project under <c>src/Modules</c> and proves
    /// the modular-monolith rules: a module never references another module, and a module only
    /// references its own context's projects plus the Platform shared kernel. It passes trivially
    /// while no modules exist (Milestone 32) and starts enforcing automatically from Milestone 33.
    /// </summary>
    [Fact]
    public void ModulesDoNotReferenceOtherModulesOrLegacyLayers()
    {
        var modulesRoot = Path.Combine(RepositoryRoot, "src", "Modules");
        if (!Directory.Exists(modulesRoot))
        {
            return;
        }

        var moduleProjects = Directory
            .EnumerateFiles(modulesRoot, "*.csproj", SearchOption.AllDirectories)
            .ToArray();

        foreach (var projectPath in moduleProjects)
        {
            var context = ContextNameOf(projectPath);
            Assert.False(
                string.IsNullOrWhiteSpace(context),
                $"Module project '{projectPath}' must follow the 'LIAnsureProtect.Modules.<Context>.<Layer>' naming.");

            foreach (var referenced in ReadReferencedProjectNames(projectPath))
            {
                // Allowed: the Platform shared kernel.
                if (referenced is "LIAnsureProtect.Platform" or "LIAnsureProtect.Platform.Abstractions")
                {
                    continue;
                }

                // Allowed: projects within the SAME module/context.
                if (referenced.StartsWith($"LIAnsureProtect.Modules.{context}.", StringComparison.Ordinal))
                {
                    continue;
                }

                // Disallowed: another module.
                Assert.False(
                    referenced.StartsWith("LIAnsureProtect.Modules.", StringComparison.Ordinal),
                    $"Module '{context}' must not reference another module ('{referenced}'). Use ids + integration events instead.");

                // Disallowed: the legacy layered projects (modules are self-contained).
                Assert.Fail(
                    $"Module '{context}' references '{referenced}', which is not its own context or the Platform shared kernel.");
            }
        }
    }

    [Fact]
    public void QuoteReferralDecisionCommandsLiveInQuotingBoundary()
    {
        var legacyDecisionPath = Path.Combine(
            RepositoryRoot,
            "src",
            "LIAnsureProtect.Application",
            "Quotes",
            "Commands",
            "UnderwriteQuoteReferral");
        var legacyDecisionFiles = Directory.Exists(legacyDecisionPath)
            ? Directory.EnumerateFiles(legacyDecisionPath, "*.cs", SearchOption.AllDirectories).ToArray()
            : [];

        var quotingDecisionPath = Path.Combine(
            RepositoryRoot,
            "src",
            "Modules",
            "Quoting",
            "LIAnsureProtect.Modules.Quoting.Application",
            "ReferralDecisions");

        Assert.Empty(legacyDecisionFiles);
        Assert.True(File.Exists(Path.Combine(quotingDecisionPath, "ApproveQuoteReferralCommand.cs")));
        Assert.True(File.Exists(Path.Combine(quotingDecisionPath, "DeclineQuoteReferralCommand.cs")));
        Assert.True(File.Exists(Path.Combine(quotingDecisionPath, "AdjustQuoteReferralCommand.cs")));
    }

    private static string[] ReadReferencedProjectNames(string fullProjectPath)
        => XDocument.Load(fullProjectPath)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            // Normalize Windows-style separators so the project name parses on Linux CI too.
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .Order()
            .ToArray();

    /// <summary>Extracts <c>Context</c> from <c>LIAnsureProtect.Modules.&lt;Context&gt;.&lt;Layer&gt;</c>.</summary>
    private static string ContextNameOf(string projectPath)
    {
        var name = Path.GetFileNameWithoutExtension(projectPath);
        const string prefix = "LIAnsureProtect.Modules.";
        if (!name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var remainder = name[prefix.Length..];
        var separator = remainder.IndexOf('.');
        return separator <= 0 ? string.Empty : remainder[..separator];
    }


    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LIAnsureProtect.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find LIAnsureProtect repository root.");
    }
}
