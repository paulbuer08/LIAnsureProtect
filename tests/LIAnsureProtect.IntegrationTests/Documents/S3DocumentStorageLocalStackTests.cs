using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using LIAnsureProtect.Infrastructure.Documents;
using LIAnsureProtect.Platform.Abstractions.Documents;
using Microsoft.Extensions.Options;

namespace LIAnsureProtect.IntegrationTests.Documents;

/// <summary>
/// Opt-in round-trip test proving the S3 adapter really stores and re-reads bytes through a live
/// S3 API — run against LocalStack (no AWS account, no bill). Skipped by default (exactly like the
/// PostgreSQL opt-in test) so the standard test/CI path stays green; enable with
/// <c>LIANSUREPROTECT_RUN_S3_TESTS=true</c> after starting LocalStack
/// (<c>docker compose --profile aws-local up -d</c>).
/// </summary>
public sealed class S3DocumentStorageLocalStackTests
{
    private const string EnabledEnvironmentVariableName = "LIANSUREPROTECT_RUN_S3_TESTS";
    private const string ServiceUrlEnvironmentVariableName = "LIANSUREPROTECT_TEST_S3_SERVICE_URL";
    private const string BucketEnvironmentVariableName = "LIANSUREPROTECT_TEST_S3_BUCKET";
    private const string DefaultServiceUrl = "http://localhost:4566";
    private const string DefaultBucketName = "liansureprotect-evidence-test";

    [Fact]
    public async Task Stores_And_Reads_Back_Document_Through_LocalStack()
    {
        Assert.SkipUnless(
            S3TestsAreEnabled(),
            $"Set {EnabledEnvironmentVariableName}=true (and start LocalStack) to run S3-backed integration tests.");

        var bucketName = GetBucketName();
        using var s3Client = new AmazonS3Client(
            "test",
            "test",
            new AmazonS3Config
            {
                ServiceURL = GetServiceUrl(),
                ForcePathStyle = true
            });

        await EnsureBucketExistsAsync(s3Client, bucketName, TestContext.Current.CancellationToken);

        var service = new S3DocumentStorageService(
            s3Client,
            Options.Create(new DocumentStorageOptions
            {
                S3 = new S3DocumentStorageOptions { BucketName = bucketName }
            }));

        var originalBytes = Encoding.UTF8.GetBytes($"round-trip evidence {Guid.NewGuid():N}");
        await using var uploadStream = new MemoryStream(originalBytes);

        var stored = await service.StoreAsync(
            new DocumentStorageUpload("evidence.pdf", "application/pdf", uploadStream),
            TestContext.Current.CancellationToken);

        var download = await service.OpenReadAsync(stored.StorageKey, TestContext.Current.CancellationToken);

        Assert.NotNull(download);
        await using var downloadStream = download!.Content;
        using var buffer = new MemoryStream();
        await downloadStream.CopyToAsync(buffer, TestContext.Current.CancellationToken);

        Assert.Equal(originalBytes, buffer.ToArray());
        Assert.StartsWith("evidence-documents/", stored.StorageKey);

        // A key that was never stored reads back as null (the download-gate contract).
        var missing = await service.OpenReadAsync(
            "evidence-documents/does-not-exist.pdf",
            TestContext.Current.CancellationToken);
        Assert.Null(missing);
    }

    private static async Task EnsureBucketExistsAsync(
        AmazonS3Client s3Client,
        string bucketName,
        CancellationToken cancellationToken)
    {
        var buckets = await s3Client.ListBucketsAsync(cancellationToken);
        if (buckets.Buckets is not null && buckets.Buckets.Any(bucket => bucket.BucketName == bucketName))
            return;

        await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName }, cancellationToken);
    }

    private static bool S3TestsAreEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(EnabledEnvironmentVariableName),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetServiceUrl()
    {
        return Environment.GetEnvironmentVariable(ServiceUrlEnvironmentVariableName) ?? DefaultServiceUrl;
    }

    private static string GetBucketName()
    {
        return Environment.GetEnvironmentVariable(BucketEnvironmentVariableName) ?? DefaultBucketName;
    }
}
