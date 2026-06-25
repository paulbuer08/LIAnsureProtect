using System.Xml.Linq;

namespace LIAnsureProtect.UnitTests.Architecture;


public sealed class ProjectReferenceBoundaryTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Theory]
    [InlineData("src/LIAnsureProtect.Domain/LIAnsureProtect.Domain.csproj")]
    [InlineData("src/LIAnsureProtect.Application/LIAnsureProtect.Application.csproj", "LIAnsureProtect.Domain")]
    [InlineData(
        "src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj",
        "LIAnsureProtect.Application",
        "LIAnsureProtect.Domain")]
    [InlineData(
        "src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj",
        "LIAnsureProtect.Application",
        "LIAnsureProtect.Infrastructure")]
    [InlineData(
        "src/LIAnsureProtect.Worker/LIAnsureProtect.Worker.csproj",
        "LIAnsureProtect.Application",
        "LIAnsureProtect.Infrastructure")]
    public void ProjectReferencesFollowCleanArchitectureDirection(
        string projectPath,
        params string[] expectedReferencedProjects)
    {
        // Arrange
        var fullProjectPath = Path.Combine(RepositoryRoot, projectPath);

        // Act
        var actualReferencedProjects = XDocument.Load(fullProjectPath)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            // Normalize Windows-style separators so the project name parses on Linux CI too.
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .Order()
            .ToArray();

        // Assert
        Assert.Equal(expectedReferencedProjects.Order().ToArray(), actualReferencedProjects);
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
