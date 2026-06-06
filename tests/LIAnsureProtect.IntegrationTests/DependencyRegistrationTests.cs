using LIAnsureProtect.Application;
using LIAnsureProtect.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.IntegrationTests;


public sealed class DependencyRegistrationTests
{
    [Fact]
    public void ApplicationAndInfrastructureRegistrationCanBeComposed()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();
        services.AddInfrastructure();
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider);
    }
}
