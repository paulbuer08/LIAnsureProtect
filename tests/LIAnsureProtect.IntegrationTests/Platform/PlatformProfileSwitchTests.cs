using LIAnsureProtect.Infrastructure;
using LIAnsureProtect.Infrastructure.Documents;
using LIAnsureProtect.Platform;
using LIAnsureProtect.Platform.Abstractions;
using LIAnsureProtect.Platform.Abstractions.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.IntegrationTests.Platform;

/// <summary>
/// Proves the Local &#8644; AWS deploy switch on its first concrete port (document storage):
/// the active <see cref="PlatformProfile"/> selects the adapter, and an unimplemented profile
/// fails fast at composition rather than silently mis-wiring.
/// </summary>
public sealed class PlatformProfileSwitchTests
{
    private const string TestConnectionString =
        "Host=localhost;Database=liansureprotect_test;Username=postgres;Password=postgres";

    [Fact]
    public void LocalProfileWiresTheLocalDocumentStorageAdapter()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(TestConnectionString, PlatformProfile.Local);

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IDocumentStorageService>();

        Assert.IsType<LocalDocumentStorageService>(storage);
    }

    [Fact]
    public void AwsProfileWiresTheS3DocumentStorageAdapter()
    {
        var services = new ServiceCollection();
        services.Configure<DocumentStorageOptions>(options => options.S3 = new S3DocumentStorageOptions
        {
            BucketName = "liansureprotect-evidence-test",
            ServiceUrl = "http://localhost:4566",
            ForcePathStyle = true,
            AccessKeyId = "test",
            SecretAccessKey = "test"
        });
        services.AddInfrastructure(TestConnectionString, PlatformProfile.Aws);

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IDocumentStorageService>();

        Assert.IsType<S3DocumentStorageService>(storage);
    }

    [Fact]
    public void AwsProfileFailsFastWhenBucketMissing()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(TestConnectionString, PlatformProfile.Aws);

        using var provider = services.BuildServiceProvider();

        // No DocumentStorage:S3 configured → resolving the storage adapter must fail fast rather
        // than silently mis-wire a bucketless S3 client.
        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IDocumentStorageService>());
    }

    [Theory]
    [InlineData(null, PlatformProfile.Local)]
    [InlineData("", PlatformProfile.Local)]
    [InlineData("Local", PlatformProfile.Local)]
    [InlineData("local", PlatformProfile.Local)]
    [InlineData("Aws", PlatformProfile.Aws)]
    public void ResolverReadsTheConfiguredProfile(string? configured, PlatformProfile expected)
    {
        var configuration = BuildConfiguration(configured);

        Assert.Equal(expected, PlatformProfileResolver.Resolve(configuration));
    }

    [Fact]
    public void ResolverRejectsAnUnknownProfile()
    {
        var configuration = BuildConfiguration("Azure");

        Assert.Throws<InvalidOperationException>(() => PlatformProfileResolver.Resolve(configuration));
    }

    private static IConfiguration BuildConfiguration(string? profileValue)
    {
        var values = new Dictionary<string, string?>();
        if (profileValue is not null)
        {
            values[$"{PlatformOptions.SectionName}:{nameof(PlatformOptions.Profile)}"] = profileValue;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
