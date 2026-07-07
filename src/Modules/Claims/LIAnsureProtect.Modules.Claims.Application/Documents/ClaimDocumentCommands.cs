using LIAnsureProtect.Modules.Claims.Domain;
using LIAnsureProtect.Platform.Abstractions.Documents;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Claims.Application.Documents;

/// <summary>
/// The claimant uploads supporting documents to their claim. Every file is stored privately
/// (platform storage port), quarantine-scanned, and persisted with its scan outcome. Owner-scoped:
/// a claim that is missing or owned by someone else returns null (→ 404).
/// </summary>
public sealed record UploadClaimDocumentsCommand(
    Guid ClaimId,
    ClaimDocumentKind Kind,
    IReadOnlyCollection<ClaimDocumentUpload> Documents) : IRequest<UploadClaimDocumentsResult?>;

public sealed record ClaimDocumentUpload(
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    Stream Content);

public sealed record UploadClaimDocumentsResult(
    Guid ClaimId,
    IReadOnlyCollection<ClaimDocumentResult> Documents);

public sealed record ClaimDocumentResult(
    Guid DocumentId,
    Guid ClaimId,
    string Kind,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string ScanStatus,
    string? ScanResultReason,
    bool IsDownloadAvailable,
    string UploadedByUserId,
    DateTime UploadedAtUtc);

public sealed record ClaimDocumentDownloadResult(
    string OriginalFileName,
    string ContentType,
    Stream Content);

public sealed record DownloadOwnerClaimDocumentQuery(
    Guid ClaimId,
    Guid DocumentId) : IRequest<ClaimDocumentDownloadResult?>;

public sealed record DownloadAdjudicationClaimDocumentQuery(
    Guid ClaimId,
    Guid DocumentId) : IRequest<ClaimDocumentDownloadResult?>;

public sealed class UploadClaimDocumentsCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser,
    IDocumentStorageService documentStorageService,
    IClaimDocumentScanner documentScanner)
    : IRequestHandler<UploadClaimDocumentsCommand, UploadClaimDocumentsResult?>
{
    public async Task<UploadClaimDocumentsResult?> Handle(
        UploadClaimDocumentsCommand request,
        CancellationToken cancellationToken)
    {
        ClaimDocumentUploadWorkflow.ValidateDocumentUploads(request.Documents);

        var claimantUserId = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to upload claim documents.")
            : currentUser.UserId;

        var claim = await claims.GetByIdForUpdateAsync(request.ClaimId, cancellationToken);
        if (claim is null || !string.Equals(claim.OwnerUserId, claimantUserId, StringComparison.Ordinal))
            return null;

        var uploadedAtUtc = DateTime.UtcNow;
        var results = new List<ClaimDocumentResult>();

        foreach (var upload in request.Documents)
        {
            var storedDocument = await documentStorageService.StoreAsync(
                new DocumentStorageUpload(upload.OriginalFileName, upload.ContentType, upload.Content),
                cancellationToken);

            var document = claim.AddDocument(
                request.Kind,
                Path.GetFileName(upload.OriginalFileName),
                upload.ContentType,
                upload.SizeBytes,
                storedDocument.StorageKey,
                claimantUserId,
                uploadedAtUtc);

            // Scan what was actually stored (not the request stream) so the verdict and the
            // SHA-256 describe the exact bytes an adjuster could later download.
            var storedDownload = await documentStorageService.OpenReadAsync(storedDocument.StorageKey, cancellationToken)
                ?? throw new InvalidOperationException("Stored claim document could not be opened for security screening.");
            await using (storedDownload.Content)
            {
                var scanResult = await documentScanner.ScanAsync(
                    new ClaimDocumentScanRequest(
                        document.OriginalFileName,
                        document.ContentType,
                        document.SizeBytes,
                        storedDownload.Content),
                    cancellationToken);

                document.RecordScanResult(
                    scanResult.ScanStatus,
                    scanResult.ScannerProviderName,
                    scanResult.ScanResultCode,
                    scanResult.ScanResultReason,
                    scanResult.Sha256,
                    scanResult.ScannedAtUtc);
            }

            results.Add(ClaimDocumentResultFactory.FromDocument(document));
        }

        await claims.SaveChangesAsync(cancellationToken);

        return new UploadClaimDocumentsResult(claim.Id, results);
    }
}

public sealed class DownloadOwnerClaimDocumentQueryHandler(
    IClaimRepository claims,
    ICurrentUser currentUser,
    IDocumentStorageService documentStorageService)
    : IRequestHandler<DownloadOwnerClaimDocumentQuery, ClaimDocumentDownloadResult?>
{
    public async Task<ClaimDocumentDownloadResult?> Handle(
        DownloadOwnerClaimDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to download claim documents.")
            : currentUser.UserId;

        var claim = await claims.GetByIdForUpdateAsync(request.ClaimId, cancellationToken);
        if (claim is null || !string.Equals(claim.OwnerUserId, ownerUserId, StringComparison.Ordinal))
            return null;

        return await ClaimDocumentDownloadWorkflow.OpenAsync(
            claim, request.DocumentId, documentStorageService, cancellationToken);
    }
}

public sealed class DownloadAdjudicationClaimDocumentQueryHandler(
    IClaimRepository claims,
    IDocumentStorageService documentStorageService)
    : IRequestHandler<DownloadAdjudicationClaimDocumentQuery, ClaimDocumentDownloadResult?>
{
    public async Task<ClaimDocumentDownloadResult?> Handle(
        DownloadAdjudicationClaimDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var claim = await claims.GetByIdForUpdateAsync(request.ClaimId, cancellationToken);
        if (claim is null)
            return null;

        return await ClaimDocumentDownloadWorkflow.OpenAsync(
            claim, request.DocumentId, documentStorageService, cancellationToken);
    }
}

public static class ClaimDocumentResultFactory
{
    public static ClaimDocumentResult FromDocument(ClaimDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new ClaimDocumentResult(
            document.Id,
            document.ClaimId,
            document.Kind.ToString(),
            document.OriginalFileName,
            document.ContentType,
            document.SizeBytes,
            document.ScanStatus.ToString(),
            document.ScanResultReason,
            document.IsDownloadAvailable,
            document.UploadedByUserId,
            document.UploadedAtUtc);
    }
}

internal static class ClaimDocumentUploadWorkflow
{
    public static void ValidateDocumentUploads(IReadOnlyCollection<ClaimDocumentUpload> documents)
    {
        if (documents.Count == 0)
            throw new ArgumentException("Claim document uploads must include at least one file.", nameof(documents));

        if (documents.Count > ClaimDocumentUploadRules.MaximumDocumentCount)
            throw new ArgumentException("Claim document uploads can include up to 5 files.", nameof(documents));

        if (documents.Sum(document => document.SizeBytes) > ClaimDocumentUploadRules.MaximumTotalDocumentSizeBytes)
            throw new ArgumentException("Claim documents cannot exceed 50 MB in total.", nameof(documents));

        foreach (var document in documents)
        {
            if (document.SizeBytes <= 0)
                throw new ArgumentException("Claim documents cannot be empty.", nameof(documents));

            if (document.SizeBytes > ClaimDocumentUploadRules.MaximumDocumentSizeBytes)
                throw new ArgumentException("Each claim document must be 10 MB or smaller.", nameof(documents));

            var fileName = Path.GetFileName(document.OriginalFileName);
            if (string.IsNullOrWhiteSpace(fileName) || fileName != document.OriginalFileName)
                throw new ArgumentException("Claim document file names must not contain path information.", nameof(documents));

            if (!ClaimDocumentUploadRules.AllowedExtensionsByContentType.TryGetValue(document.ContentType, out var expectedExtension))
                throw new ArgumentException("Claim document content type is not supported.", nameof(documents));

            var extension = Path.GetExtension(fileName);
            if (!string.Equals(extension, expectedExtension, StringComparison.OrdinalIgnoreCase)
                && !(string.Equals(document.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Claim document extension does not match the content type.", nameof(documents));
            }
        }
    }
}

internal static class ClaimDocumentDownloadWorkflow
{
    public static async Task<ClaimDocumentDownloadResult?> OpenAsync(
        Claim claim,
        Guid documentId,
        IDocumentStorageService documentStorageService,
        CancellationToken cancellationToken)
    {
        var document = claim.Documents.SingleOrDefault(candidate => candidate.Id == documentId);
        if (document is null)
            return null;

        // Fail-closed: anything not scanned Clean is refused, whatever the caller's role.
        if (!document.IsDownloadAvailable)
            throw new InvalidOperationException(
                $"Claim document scan status is {document.ScanStatus} and is not trusted for download.");

        var download = await documentStorageService.OpenReadAsync(document.StorageKey, cancellationToken);

        return download is null
            ? null
            : new ClaimDocumentDownloadResult(document.OriginalFileName, document.ContentType, download.Content);
    }
}
