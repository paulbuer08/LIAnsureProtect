namespace LIAnsureProtect.Platform.Abstractions.Documents;

public interface IDocumentStorageService
{
    Task<StoredDocumentResult> StoreAsync(
        DocumentStorageUpload upload,
        CancellationToken cancellationToken);

    Task<StoredDocumentDownload?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken);
}

public sealed record DocumentStorageUpload(
    string OriginalFileName,
    string ContentType,
    Stream Content);

public sealed record StoredDocumentResult(string StorageKey);

public sealed record StoredDocumentDownload(Stream Content, string ContentType);
