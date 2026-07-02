namespace LIAnsureProtect.Infrastructure.Documents;

public sealed class DocumentStorageOptions
{
    /// <summary>Root folder for the Local filesystem adapter (used when Platform:Profile=Local).</summary>
    public string? LocalRootPath { get; set; }

    /// <summary>S3 settings for the AWS adapter (used when Platform:Profile=Aws).</summary>
    public S3DocumentStorageOptions? S3 { get; set; }
}

public sealed class S3DocumentStorageOptions
{
    /// <summary>The private bucket documents are stored in. Required under the Aws profile.</summary>
    public string? BucketName { get; set; }

    /// <summary>
    /// Custom endpoint for a non-AWS S3-compatible service (e.g. LocalStack at
    /// <c>http://localhost:4566</c>). Leave empty to target real AWS via <see cref="Region"/>.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>Use path-style addressing (required by LocalStack). Real AWS uses virtual-host style.</summary>
    public bool ForcePathStyle { get; set; }

    /// <summary>AWS region (e.g. <c>us-east-1</c>) used when <see cref="ServiceUrl"/> is not set.</summary>
    public string? Region { get; set; }

    /// <summary>Optional KMS key id/ARN. When set, every upload is encrypted with SSE-KMS.</summary>
    public string? KmsKeyId { get; set; }

    /// <summary>
    /// Static access key for LocalStack only. Leave empty in real AWS so the default credential
    /// chain (instance/task role) is used — no static keys in the cloud.
    /// </summary>
    public string? AccessKeyId { get; set; }

    /// <summary>Static secret key paired with <see cref="AccessKeyId"/> for LocalStack only.</summary>
    public string? SecretAccessKey { get; set; }
}
