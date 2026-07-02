using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using LIAnsureProtect.Platform.Abstractions.Documents;
using Microsoft.Extensions.Options;

namespace LIAnsureProtect.Infrastructure.Documents;

/// <summary>
/// Stores private evidence documents in Amazon S3 (or an S3-compatible service such as LocalStack).
/// It implements the same <see cref="IDocumentStorageService"/> port as the local filesystem
/// adapter, so the upload/scan/download flows are unchanged — only where the bytes live differs.
/// Selected when <c>Platform:Profile=Aws</c>.
/// </summary>
public sealed class S3DocumentStorageService(
    IAmazonS3 s3Client,
    IOptions<DocumentStorageOptions> options) : IDocumentStorageService
{
    private const string EvidenceKeyPrefix = "evidence-documents/";
    private readonly S3DocumentStorageOptions s3Options = options.Value.S3
        ?? throw new InvalidOperationException("DocumentStorage:S3 configuration is required when Platform:Profile=Aws.");

    public async Task<StoredDocumentResult> StoreAsync(
        DocumentStorageUpload upload,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(Path.GetFileName(upload.OriginalFileName));
        var storageKey = $"{EvidenceKeyPrefix}{Guid.NewGuid():N}{extension}";

        var request = new PutObjectRequest
        {
            BucketName = RequireBucketName(),
            Key = storageKey,
            InputStream = upload.Content,
            ContentType = upload.ContentType,
            // The caller owns the upload stream lifetime, so don't let the SDK close it.
            AutoCloseStream = false
        };

        if (!string.IsNullOrWhiteSpace(s3Options.KmsKeyId))
        {
            // Encrypt at rest with a customer-managed KMS key when one is configured.
            request.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS;
            request.ServerSideEncryptionKeyManagementServiceKeyId = s3Options.KmsKeyId;
        }

        await s3Client.PutObjectAsync(request, cancellationToken);

        return new StoredDocumentResult(storageKey);
    }

    public async Task<StoredDocumentDownload?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await s3Client.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = RequireBucketName(),
                    Key = storageKey
                },
                cancellationToken);

            var contentType = string.IsNullOrWhiteSpace(response.Headers.ContentType)
                ? "application/octet-stream"
                : response.Headers.ContentType;

            return new StoredDocumentDownload(response.ResponseStream, contentType);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            // Missing object → null, matching the local adapter's "missing file → null" contract
            // so the download and scan gates behave identically across adapters.
            return null;
        }
    }

    private string RequireBucketName()
    {
        return string.IsNullOrWhiteSpace(s3Options.BucketName)
            ? throw new InvalidOperationException("DocumentStorage:S3:BucketName is required when Platform:Profile=Aws.")
            : s3Options.BucketName;
    }
}
