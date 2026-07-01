using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Platform.Abstractions.Security;
using LIAnsureProtect.Application.Documents;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Modules.Underwriting.Application.Evidence;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.ManageQuoteEvidenceRequests;

public sealed record RespondToQuoteEvidenceRequestCommand(
    Guid EvidenceRequestId,
    string RespondentName,
    string RespondentTitle,
    string ResponseText,
    string? AttachmentFileName,
    string? AttachmentContentType,
    long? AttachmentSizeBytes,
    IReadOnlyCollection<EvidenceDocumentUpload> Documents) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record UploadReplacementEvidenceDocumentsCommand(
    Guid EvidenceRequestId,
    IReadOnlyCollection<EvidenceDocumentUpload> Documents) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record AcceptQuoteEvidenceRequestCommand(
    Guid QuoteId,
    Guid EvidenceRequestId,
    string? ReviewNotes) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record RecordQuoteEvidenceReviewDecisionCommand(
    Guid QuoteId,
    Guid EvidenceRequestId,
    string Decision,
    string Reason,
    string? RemediationGuidance) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record ListOwnerEvidenceRequestsQuery : IRequest<ListOwnerEvidenceRequestsResult>;

public sealed record DownloadOwnerEvidenceDocumentQuery(
    Guid EvidenceRequestId,
    Guid DocumentId) : IRequest<EvidenceDocumentDownloadResult?>;

public sealed record DownloadUnderwritingEvidenceDocumentQuery(
    Guid QuoteId,
    Guid EvidenceRequestId,
    Guid DocumentId) : IRequest<EvidenceDocumentDownloadResult?>;

public sealed record ListOwnerEvidenceRequestsResult(
    IReadOnlyCollection<QuoteEvidenceRequestResult> EvidenceRequests);

public sealed record QuoteEvidenceRequestResult(
    Guid EvidenceRequestId,
    Guid QuoteId,
    Guid SubmissionId,
    string Category,
    string Title,
    string Description,
    DateTime DueAtUtc,
    string Status,
    bool IsOverdue,
    int DaysUntilDue,
    string RequestedByUserId,
    DateTime RequestedAtUtc,
    string? RespondedByUserId,
    string? RespondentName,
    string? RespondentTitle,
    string? ResponseText,
    string? AttachmentFileName,
    string? AttachmentContentType,
    long? AttachmentSizeBytes,
    DateTime? RespondedAtUtc,
    string? AcceptedByUserId,
    DateTime? AcceptedAtUtc,
    string? CancelledByUserId,
    DateTime? CancelledAtUtc,
    string? ReviewNotes,
    string ReviewDecision,
    string? ReviewReason,
    string? RemediationGuidance,
    string? ReviewedByUserId,
    DateTime? ReviewedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyCollection<QuoteEvidenceDocumentResult> Documents);

public sealed record QuoteEvidenceDocumentResult(
    Guid DocumentId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string UploadedByUserId,
    DateTime UploadedAtUtc,
    string ScanStatus,
    string? ScannerProviderName,
    string? ScanResultCode,
    string? ScanResultReason,
    DateTime? ScannedAtUtc,
    string? Sha256,
    bool IsDownloadAvailable);

public sealed record EvidenceDocumentUpload(
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    Stream Content);

public sealed record EvidenceDocumentDownloadResult(
    string OriginalFileName,
    string ContentType,
    Stream Content);

public sealed class RespondToQuoteEvidenceRequestCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IEvidenceRequestsReader evidenceRequestsReader,
    IEvidenceRequestWriter evidenceRequestWriter,
    IDocumentStorageService documentStorageService,
    IEvidenceDocumentScanner evidenceDocumentScanner)
    : IRequestHandler<RespondToQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        RespondToQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        EvidenceDocumentUploadWorkflow.ValidateDocumentUploads(request.Documents);

        var ownerUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated owner user id is required to respond to evidence requests.");
        var evidenceRequest = await evidenceRequestsReader.GetOwnerRequestAsync(
            request.EvidenceRequestId,
            ownerUserId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var respondedAtUtc = DateTime.UtcNow;
        var evidenceDocuments = await EvidenceDocumentUploadWorkflow.StoreAndScanDocumentsAsync(
            request.Documents,
            EvidenceDocumentRequestFacts.FromSnapshot(evidenceRequest),
            ownerUserId,
            respondedAtUtc,
            documentStorageService,
            evidenceDocumentScanner,
            cancellationToken);

        if (evidenceDocuments.Count > 0)
        {
            await quoteRepository.AddEvidenceDocumentsAsync(evidenceDocuments, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var updatedRequest = await evidenceRequestWriter.RecordResponseAsync(
            request.EvidenceRequestId,
            ownerUserId,
            request.RespondentName,
            request.RespondentTitle,
            request.ResponseText,
            evidenceDocuments.FirstOrDefault()?.OriginalFileName ?? request.AttachmentFileName,
            evidenceDocuments.FirstOrDefault()?.ContentType ?? request.AttachmentContentType,
            evidenceDocuments.FirstOrDefault()?.SizeBytes ?? request.AttachmentSizeBytes,
            respondedAtUtc,
            cancellationToken);
        if (updatedRequest is null)
            return null;

        return QuoteEvidenceRequestResultFactory.FromSnapshot(updatedRequest, evidenceDocuments);
    }
}

public sealed class UploadReplacementEvidenceDocumentsCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IEvidenceRequestsReader evidenceRequestsReader,
    IEvidenceRequestWriter evidenceRequestWriter,
    IDocumentStorageService documentStorageService,
    IEvidenceDocumentScanner evidenceDocumentScanner)
    : IRequestHandler<UploadReplacementEvidenceDocumentsCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        UploadReplacementEvidenceDocumentsCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Documents.Count == 0)
            throw new ArgumentException("Replacement evidence uploads must include at least one file.", nameof(request.Documents));

        EvidenceDocumentUploadWorkflow.ValidateDocumentUploads(request.Documents);

        var ownerUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated owner user id is required to upload replacement evidence documents.");
        var evidenceRequest = await evidenceRequestsReader.GetOwnerRequestAsync(
            request.EvidenceRequestId,
            ownerUserId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        if (!string.Equals(evidenceRequest.Status, EvidenceRequestStatuses.Responded, StringComparison.Ordinal))
            throw new InvalidOperationException("Replacement evidence documents can only be uploaded after an evidence response.");

        var existingDocuments = await quoteRepository.ListEvidenceDocumentsForRequestsAsync(
            [evidenceRequest.EvidenceRequestId],
            cancellationToken);
        if (!existingDocuments.Any(document => document.ScanStatus is EvidenceDocumentScanStatus.Rejected or EvidenceDocumentScanStatus.Failed))
            throw new InvalidOperationException("Replacement evidence documents are only allowed after a rejected or failed security scan.");

        var uploadedAtUtc = DateTime.UtcNow;
        var replacementDocuments = await EvidenceDocumentUploadWorkflow.StoreAndScanDocumentsAsync(
            request.Documents,
            EvidenceDocumentRequestFacts.FromSnapshot(evidenceRequest),
            ownerUserId,
            uploadedAtUtc,
            documentStorageService,
            evidenceDocumentScanner,
            cancellationToken);

        if (replacementDocuments.Count > 0)
        {
            await quoteRepository.AddEvidenceDocumentsAsync(replacementDocuments, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var updatedRequest = await evidenceRequestWriter.RecordSupplementalResponseAsync(
            request.EvidenceRequestId,
            ownerUserId,
            evidenceRequest.RespondentName ?? string.Empty,
            evidenceRequest.RespondentTitle ?? string.Empty,
            evidenceRequest.ResponseText ?? string.Empty,
            replacementDocuments.FirstOrDefault()?.OriginalFileName ?? evidenceRequest.AttachmentFileName,
            replacementDocuments.FirstOrDefault()?.ContentType ?? evidenceRequest.AttachmentContentType,
            replacementDocuments.FirstOrDefault()?.SizeBytes ?? evidenceRequest.AttachmentSizeBytes,
            uploadedAtUtc,
            cancellationToken);
        if (updatedRequest is null)
            return null;

        return QuoteEvidenceRequestResultFactory.FromSnapshot(
            updatedRequest,
            existingDocuments.Concat(replacementDocuments).ToList());
    }
}

public sealed class AcceptQuoteEvidenceRequestCommandHandler(
    IQuoteRepository quoteRepository,
    ICurrentUser currentUser,
    IEvidenceRequestsReader evidenceRequestsReader,
    IEvidenceRequestWriter evidenceRequestWriter)
    : IRequestHandler<AcceptQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        AcceptQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await evidenceRequestsReader.GetUnderwritingRequestAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var documents = await quoteRepository.ListEvidenceDocumentsForRequestsAsync(
            [evidenceRequest.EvidenceRequestId],
            cancellationToken);
        EvidenceReviewDocumentGate.EnsureReviewDocumentsAreTrusted(
            documents,
            "Only clean evidence documents can be accepted.");

        var underwriterUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated underwriter user id is required to accept evidence.");
        var acceptedAtUtc = DateTime.UtcNow;

        var updatedRequest = await evidenceRequestWriter.AcceptAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            underwriterUserId,
            request.ReviewNotes,
            documents.Count,
            documents.Count(document => document.ScanStatus == EvidenceDocumentScanStatus.Clean),
            acceptedAtUtc,
            cancellationToken);
        if (updatedRequest is null)
            return null;

        return QuoteEvidenceRequestResultFactory.FromSnapshot(updatedRequest, documents);
    }
}

public sealed class RecordQuoteEvidenceReviewDecisionCommandHandler(
    IQuoteRepository quoteRepository,
    ICurrentUser currentUser,
    IEvidenceRequestsReader evidenceRequestsReader,
    IEvidenceRequestWriter evidenceRequestWriter)
    : IRequestHandler<RecordQuoteEvidenceReviewDecisionCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        RecordQuoteEvidenceReviewDecisionCommand request,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await evidenceRequestsReader.GetUnderwritingRequestAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var documents = await quoteRepository.ListEvidenceDocumentsForRequestsAsync(
            [evidenceRequest.EvidenceRequestId],
            cancellationToken);
        EvidenceReviewDocumentGate.EnsureReviewDocumentsAreTrusted(
            documents,
            "Only clean evidence documents can support a review decision.");

        var underwriterUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated underwriter user id is required to review evidence.");
        var reviewedAtUtc = DateTime.UtcNow;

        EvidenceRequestSnapshot? updatedRequest;
        if (string.Equals(request.Decision, EvidenceReviewDecisionStatuses.Satisfied, StringComparison.Ordinal))
        {
            updatedRequest = await evidenceRequestWriter.AcceptAsync(
                request.QuoteId,
                request.EvidenceRequestId,
                underwriterUserId,
                request.Reason,
                documents.Count,
                documents.Count(document => document.ScanStatus == EvidenceDocumentScanStatus.Clean),
                reviewedAtUtc,
                cancellationToken);
        }
        else
        {
            updatedRequest = await evidenceRequestWriter.RecordReviewDecisionAsync(
                request.QuoteId,
                request.EvidenceRequestId,
                request.Decision,
                request.Reason,
                request.RemediationGuidance,
                underwriterUserId,
                documents.Count,
                documents.Count(document => document.ScanStatus == EvidenceDocumentScanStatus.Clean),
                reviewedAtUtc,
                cancellationToken);
        }
        if (updatedRequest is null)
            return null;

        return QuoteEvidenceRequestResultFactory.FromSnapshot(updatedRequest, documents);
    }
}

public sealed class ListOwnerEvidenceRequestsQueryHandler(
    IQuoteRepository quoteRepository,
    IEvidenceRequestsReader evidenceRequestsReader,
    ICurrentUser currentUser)
    : IRequestHandler<ListOwnerEvidenceRequestsQuery, ListOwnerEvidenceRequestsResult>
{
    public async Task<ListOwnerEvidenceRequestsResult> Handle(
        ListOwnerEvidenceRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated owner user id is required to list evidence requests.");
        var evidenceRequests = await evidenceRequestsReader.GetOwnerRequestsAsync(
            ownerUserId,
            cancellationToken);
        var documents = await quoteRepository.ListEvidenceDocumentsForRequestsAsync(
            evidenceRequests.Select(evidenceRequest => evidenceRequest.EvidenceRequestId).ToList(),
            cancellationToken);
        var documentsByRequestId = documents
            .GroupBy(document => document.EvidenceRequestId)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<QuoteEvidenceDocument>)group.ToList());

        return new ListOwnerEvidenceRequestsResult(
            evidenceRequests
                .Select(evidenceRequest => QuoteEvidenceRequestResultFactory.FromOwnerItem(
                    evidenceRequest,
                    documentsByRequestId.GetValueOrDefault(evidenceRequest.EvidenceRequestId) ?? []))
                .ToList());
    }
}

public sealed class DownloadOwnerEvidenceDocumentQueryHandler(
    IQuoteRepository quoteRepository,
    ICurrentUser currentUser,
    IDocumentStorageService documentStorageService)
    : IRequestHandler<DownloadOwnerEvidenceDocumentQuery, EvidenceDocumentDownloadResult?>
{
    public async Task<EvidenceDocumentDownloadResult?> Handle(
        DownloadOwnerEvidenceDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated owner user id is required to download evidence documents.");
        var document = await quoteRepository.GetEvidenceDocumentForOwnerAsync(
            request.EvidenceRequestId,
            request.DocumentId,
            ownerUserId,
            cancellationToken);
        if (document is null)
            return null;

        return await EvidenceDocumentDownloadResultFactory.OpenDocumentAsync(
            documentStorageService,
            document,
            cancellationToken);
    }
}

public sealed class DownloadUnderwritingEvidenceDocumentQueryHandler(
    IQuoteRepository quoteRepository,
    IDocumentStorageService documentStorageService)
    : IRequestHandler<DownloadUnderwritingEvidenceDocumentQuery, EvidenceDocumentDownloadResult?>
{
    public async Task<EvidenceDocumentDownloadResult?> Handle(
        DownloadUnderwritingEvidenceDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var document = await quoteRepository.GetEvidenceDocumentForUnderwritingAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            request.DocumentId,
            cancellationToken);
        if (document is null)
            return null;

        return await EvidenceDocumentDownloadResultFactory.OpenDocumentAsync(
            documentStorageService,
            document,
            cancellationToken);
    }
}

public static class QuoteEvidenceRequestResultFactory
{
    public static QuoteEvidenceRequestResult FromRequest(
        QuoteEvidenceRequest request,
        IReadOnlyCollection<QuoteEvidenceDocument>? documents = null)
    {
        return new QuoteEvidenceRequestResult(
            request.Id,
            request.QuoteId,
            request.SubmissionId,
            request.Category.ToString(),
            request.Title,
            request.Description,
            request.DueAtUtc,
            request.Status.ToString(),
            request.Status == EvidenceRequestStatus.Open && request.DueAtUtc < DateTime.UtcNow,
            (request.DueAtUtc.Date - DateTime.UtcNow.Date).Days,
            request.RequestedByUserId,
            request.RequestedAtUtc,
            request.RespondedByUserId,
            request.RespondentName,
            request.RespondentTitle,
            request.ResponseText,
            request.AttachmentFileName,
            request.AttachmentContentType,
            request.AttachmentSizeBytes,
            request.RespondedAtUtc,
            request.AcceptedByUserId,
            request.AcceptedAtUtc,
            request.CancelledByUserId,
            request.CancelledAtUtc,
            request.ReviewNotes,
            request.ReviewDecision.ToString(),
            request.ReviewReason,
            request.RemediationGuidance,
            request.ReviewedByUserId,
            request.ReviewedAtUtc,
            request.UpdatedAtUtc,
            (documents ?? [])
                .OrderBy(document => document.UploadedAtUtc)
                .Select(FromDocument)
                .ToList());
    }

    public static QuoteEvidenceRequestResult FromSnapshot(
        EvidenceRequestSnapshot request,
        IReadOnlyCollection<QuoteEvidenceDocument>? documents = null)
    {
        return new QuoteEvidenceRequestResult(
            request.EvidenceRequestId,
            request.QuoteId,
            request.SubmissionId,
            request.Category,
            request.Title,
            request.Description,
            request.DueAtUtc,
            request.Status,
            request.IsOverdue,
            request.DaysUntilDue,
            request.RequestedByUserId,
            request.RequestedAtUtc,
            request.RespondedByUserId,
            request.RespondentName,
            request.RespondentTitle,
            request.ResponseText,
            request.AttachmentFileName,
            request.AttachmentContentType,
            request.AttachmentSizeBytes,
            request.RespondedAtUtc,
            request.AcceptedByUserId,
            request.AcceptedAtUtc,
            request.CancelledByUserId,
            request.CancelledAtUtc,
            request.ReviewNotes,
            request.ReviewDecision,
            request.ReviewReason,
            request.RemediationGuidance,
            request.ReviewedByUserId,
            request.ReviewedAtUtc,
            request.UpdatedAtUtc,
            (documents ?? [])
                .OrderBy(document => document.UploadedAtUtc)
                .Select(FromDocument)
                .ToList());
    }

    public static QuoteEvidenceRequestResult FromOwnerItem(
        EvidenceRequestOwnerItem request,
        IReadOnlyCollection<QuoteEvidenceDocument>? documents = null)
    {
        return new QuoteEvidenceRequestResult(
            request.EvidenceRequestId,
            request.QuoteId,
            request.SubmissionId,
            request.Category,
            request.Title,
            request.Description,
            request.DueAtUtc,
            request.Status,
            request.IsOverdue,
            request.DaysUntilDue,
            request.RequestedByUserId,
            request.RequestedAtUtc,
            request.RespondedByUserId,
            request.RespondentName,
            request.RespondentTitle,
            request.ResponseText,
            request.AttachmentFileName,
            request.AttachmentContentType,
            request.AttachmentSizeBytes,
            request.RespondedAtUtc,
            request.AcceptedByUserId,
            request.AcceptedAtUtc,
            request.CancelledByUserId,
            request.CancelledAtUtc,
            request.ReviewNotes,
            request.ReviewDecision,
            request.ReviewReason,
            request.RemediationGuidance,
            request.ReviewedByUserId,
            request.ReviewedAtUtc,
            request.UpdatedAtUtc,
            (documents ?? [])
                .OrderBy(document => document.UploadedAtUtc)
                .Select(FromDocument)
                .ToList());
    }

    private static QuoteEvidenceDocumentResult FromDocument(QuoteEvidenceDocument document)
    {
        return new QuoteEvidenceDocumentResult(
            document.Id,
            document.OriginalFileName,
            document.ContentType,
            document.SizeBytes,
            document.UploadedByUserId,
            document.UploadedAtUtc,
            document.ScanStatus.ToString(),
            document.ScannerProviderName,
            document.ScanResultCode,
            document.ScanResultReason,
            document.ScannedAtUtc,
            document.Sha256,
            document.IsDownloadAvailable);
    }
}

internal sealed record EvidenceDocumentRequestFacts(
    Guid EvidenceRequestId,
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId)
{
    public static EvidenceDocumentRequestFacts FromSnapshot(EvidenceRequestSnapshot snapshot)
    {
        return new EvidenceDocumentRequestFacts(
            snapshot.EvidenceRequestId,
            snapshot.QuoteId,
            snapshot.SubmissionId,
            snapshot.OwnerUserId);
    }
}

internal static class EvidenceRequestStatuses
{
    public const string Responded = "Responded";
}

internal static class EvidenceReviewDecisionStatuses
{
    public const string Satisfied = "Satisfied";
}

internal static class EvidenceReviewDocumentGate
{
    public static void EnsureReviewDocumentsAreTrusted(
        IReadOnlyCollection<QuoteEvidenceDocument> documents,
        string message)
    {
        if (documents.Any(document => !document.IsDownloadAvailable))
            throw new InvalidOperationException(message);
    }
}

internal static class EvidenceDocumentDownloadResultFactory
{
    public static async Task<EvidenceDocumentDownloadResult?> OpenDocumentAsync(
        IDocumentStorageService documentStorageService,
        QuoteEvidenceDocument document,
        CancellationToken cancellationToken)
    {
        if (!document.IsDownloadAvailable)
            throw new InvalidOperationException($"Evidence document scan status is {document.ScanStatus} and is not trusted for download.");

        var download = await documentStorageService.OpenReadAsync(document.StorageKey, cancellationToken);
        return download is null
            ? null
            : new EvidenceDocumentDownloadResult(
                document.OriginalFileName,
                document.ContentType,
                download.Content);
    }
}

internal static class CurrentUserId
{
    public static string GetRequired(ICurrentUser currentUser, string message)
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException(message)
            : currentUser.UserId;
    }
}

internal static class EvidenceDocumentUploadWorkflow
{
    private const int MaximumDocumentCount = 5;
    private const long MaximumDocumentSizeBytes = 10 * 1024 * 1024;
    private const long MaximumTotalDocumentSizeBytes = 50 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AllowedExtensionsByContentType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = ".pdf",
            ["image/png"] = ".png",
            ["image/jpeg"] = ".jpg",
            ["text/plain"] = ".txt",
            ["text/csv"] = ".csv",
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx"
        };

    public static void ValidateDocumentUploads(IReadOnlyCollection<EvidenceDocumentUpload> documents)
    {
        if (documents.Count > MaximumDocumentCount)
            throw new ArgumentException($"Evidence responses can include up to 5 files.", nameof(documents));

        if (documents.Sum(document => document.SizeBytes) > MaximumTotalDocumentSizeBytes)
            throw new ArgumentException("Evidence response documents cannot exceed 50 MB in total.", nameof(documents));

        foreach (var document in documents)
        {
            if (document.SizeBytes <= 0)
                throw new ArgumentException("Evidence documents cannot be empty.", nameof(documents));

            if (document.SizeBytes > MaximumDocumentSizeBytes)
                throw new ArgumentException("Each evidence document must be 10 MB or smaller.", nameof(documents));

            var fileName = Path.GetFileName(document.OriginalFileName);
            if (string.IsNullOrWhiteSpace(fileName) || fileName != document.OriginalFileName)
                throw new ArgumentException("Evidence document file names must not contain path information.", nameof(documents));

            if (!AllowedExtensionsByContentType.TryGetValue(document.ContentType, out var expectedExtension))
                throw new ArgumentException("Evidence document content type is not supported.", nameof(documents));

            var extension = Path.GetExtension(fileName);
            if (!string.Equals(extension, expectedExtension, StringComparison.OrdinalIgnoreCase)
                && !(string.Equals(document.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Evidence document extension does not match the content type.", nameof(documents));
            }
        }
    }

    public static async Task<IReadOnlyCollection<QuoteEvidenceDocument>> StoreAndScanDocumentsAsync(
        IReadOnlyCollection<EvidenceDocumentUpload> uploads,
        EvidenceDocumentRequestFacts evidenceRequest,
        string uploadedByUserId,
        DateTime uploadedAtUtc,
        IDocumentStorageService documentStorageService,
        IEvidenceDocumentScanner evidenceDocumentScanner,
        CancellationToken cancellationToken)
    {
        var evidenceDocuments = new List<QuoteEvidenceDocument>();

        foreach (var upload in uploads)
        {
            var storedDocument = await documentStorageService.StoreAsync(
                new DocumentStorageUpload(
                    upload.OriginalFileName,
                    upload.ContentType,
                    upload.Content),
                cancellationToken);

            var evidenceDocument = QuoteEvidenceDocument.Create(
                evidenceRequest.EvidenceRequestId,
                evidenceRequest.QuoteId,
                evidenceRequest.SubmissionId,
                evidenceRequest.OwnerUserId,
                Path.GetFileName(upload.OriginalFileName),
                upload.ContentType,
                upload.SizeBytes,
                storedDocument.StorageKey,
                uploadedByUserId,
                uploadedAtUtc);

            var storedDownload = await documentStorageService.OpenReadAsync(storedDocument.StorageKey, cancellationToken)
                ?? throw new InvalidOperationException("Stored evidence document could not be opened for security screening.");
            await using (storedDownload.Content)
            {
                var scanResult = await evidenceDocumentScanner.ScanAsync(
                    new EvidenceDocumentScanRequest(
                        evidenceDocument.OriginalFileName,
                        evidenceDocument.ContentType,
                        evidenceDocument.SizeBytes,
                        storedDownload.Content),
                    cancellationToken);

                evidenceDocument.RecordScanResult(
                    scanResult.ScanStatus,
                    scanResult.ScannerProviderName,
                    scanResult.ScanResultCode,
                    scanResult.ScanResultReason,
                    scanResult.Sha256,
                    scanResult.ScannedAtUtc);
            }

            evidenceDocuments.Add(evidenceDocument);
        }

        return evidenceDocuments;
    }
}
