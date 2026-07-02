using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using LIAnsureProtect.Infrastructure.Documents;
using LIAnsureProtect.Platform.Abstractions.Documents;
using Microsoft.Extensions.Options;
using Moq;

namespace LIAnsureProtect.IntegrationTests.Documents;

/// <summary>
/// Unit-level tests for the S3 document storage adapter. They mock <see cref="IAmazonS3"/> and
/// assert on the request we build and the response we map — our logic, not the SDK's. No network,
/// no Docker, so they run in the normal test/CI path. A real S3 round trip is proven separately by
/// the opt-in LocalStack test.
/// </summary>
public sealed class S3DocumentStorageServiceTests
{
    private const string BucketName = "liansureprotect-evidence-test";

    [Fact]
    public async Task StoreAsync_Puts_Object_Under_Evidence_Prefix_And_Returns_Storage_Key()
    {
        PutObjectRequest? capturedRequest = null;
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutObjectResponse());

        var service = CreateService(s3.Object);
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("evidence bytes"));

        var result = await service.StoreAsync(
            new DocumentStorageUpload("mfa-policy.pdf", "application/pdf", content),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capturedRequest);
        Assert.Equal(BucketName, capturedRequest!.BucketName);
        Assert.StartsWith("evidence-documents/", capturedRequest.Key);
        Assert.EndsWith(".pdf", capturedRequest.Key);
        Assert.Equal("application/pdf", capturedRequest.ContentType);
        Assert.Equal(capturedRequest.Key, result.StorageKey);
        // No KMS key configured → no server-side encryption override on the request.
        Assert.Null(capturedRequest.ServerSideEncryptionKeyManagementServiceKeyId);
    }

    [Fact]
    public async Task StoreAsync_Applies_Sse_Kms_When_Key_Configured()
    {
        PutObjectRequest? capturedRequest = null;
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutObjectResponse());

        var service = CreateService(s3.Object, kmsKeyId: "arn:aws:kms:us-east-1:111122223333:key/evidence");
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("evidence bytes"));

        await service.StoreAsync(
            new DocumentStorageUpload("mfa-policy.pdf", "application/pdf", content),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capturedRequest);
        Assert.Equal(ServerSideEncryptionMethod.AWSKMS, capturedRequest!.ServerSideEncryptionMethod);
        Assert.Equal(
            "arn:aws:kms:us-east-1:111122223333:key/evidence",
            capturedRequest.ServerSideEncryptionKeyManagementServiceKeyId);
    }

    [Fact]
    public async Task OpenReadAsync_Returns_Object_Stream_And_Content_Type()
    {
        var payload = Encoding.UTF8.GetBytes("stored evidence");
        var response = new GetObjectResponse { ResponseStream = new MemoryStream(payload) };
        response.Headers.ContentType = "application/pdf";

        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.GetObjectAsync(
                It.Is<GetObjectRequest>(request => request.BucketName == BucketName
                    && request.Key == "evidence-documents/abc.pdf"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var service = CreateService(s3.Object);

        var download = await service.OpenReadAsync(
            "evidence-documents/abc.pdf",
            TestContext.Current.CancellationToken);

        Assert.NotNull(download);
        Assert.Equal("application/pdf", download!.ContentType);
        using var reader = new StreamReader(download.Content);
        Assert.Equal("stored evidence", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OpenReadAsync_Returns_Null_When_Object_Missing()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("The specified key does not exist.")
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var service = CreateService(s3.Object);

        var download = await service.OpenReadAsync(
            "evidence-documents/missing.pdf",
            TestContext.Current.CancellationToken);

        Assert.Null(download);
    }

    private static S3DocumentStorageService CreateService(IAmazonS3 s3Client, string? kmsKeyId = null)
    {
        var options = Options.Create(new DocumentStorageOptions
        {
            S3 = new S3DocumentStorageOptions
            {
                BucketName = BucketName,
                KmsKeyId = kmsKeyId
            }
        });

        return new S3DocumentStorageService(s3Client, options);
    }
}
