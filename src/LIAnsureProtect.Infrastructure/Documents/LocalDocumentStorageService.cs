using LIAnsureProtect.Platform.Abstractions.Documents;
using Microsoft.Extensions.Options;

namespace LIAnsureProtect.Infrastructure.Documents;

public sealed class LocalDocumentStorageService(IOptions<DocumentStorageOptions> options)
    : IDocumentStorageService
{
    private readonly string rootPath = ResolveRootPath(options.Value.LocalRootPath);

    public async Task<StoredDocumentResult> StoreAsync(
        DocumentStorageUpload upload,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(Path.GetFileName(upload.OriginalFileName));
        var storageKey = $"evidence-documents/{Guid.NewGuid():N}{extension}";
        var destinationPath = ResolveStoragePath(storageKey);
        var destinationDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("Document storage path is invalid.");

        Directory.CreateDirectory(destinationDirectory);

        await using var output = File.Create(destinationPath);
        await upload.Content.CopyToAsync(output, cancellationToken);

        return new StoredDocumentResult(storageKey);
    }

    public Task<StoredDocumentDownload?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        var path = ResolveStoragePath(storageKey);
        if (!File.Exists(path))
            return Task.FromResult<StoredDocumentDownload?>(null);

        Stream stream = File.OpenRead(path);
        return Task.FromResult<StoredDocumentDownload?>(new StoredDocumentDownload(stream, "application/octet-stream"));
    }

    private string ResolveStoragePath(string storageKey)
    {
        var path = Path.GetFullPath(Path.Combine(rootPath, storageKey));
        var root = Path.GetFullPath(rootPath);
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Document storage key resolves outside the configured storage root.");
        }

        return path;
    }

    private static string ResolveRootPath(string? configuredRootPath)
    {
        var rootPath = string.IsNullOrWhiteSpace(configuredRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "evidence-documents")
            : configuredRootPath;

        return Path.GetFullPath(rootPath);
    }
}
