using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Application.Documents;
using LIAnsureProtect.Domain.Quotes;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.ManageQuoteEvidenceRequests;

public sealed record CreateQuoteEvidenceRequestCommand(
    Guid QuoteId,
    EvidenceRequestCategory Category,
    string Title,
    string Description,
    DateTime DueAtUtc) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record RespondToQuoteEvidenceRequestCommand(
    Guid EvidenceRequestId,
    string RespondentName,
    string RespondentTitle,
    string ResponseText,
    string? AttachmentFileName,
    string? AttachmentContentType,
    long? AttachmentSizeBytes,
    IReadOnlyCollection<EvidenceDocumentUpload> Documents) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record AcceptQuoteEvidenceRequestCommand(
    Guid QuoteId,
    Guid EvidenceRequestId,
    string? ReviewNotes) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record CancelQuoteEvidenceRequestCommand(
    Guid QuoteId,
    Guid EvidenceRequestId,
    string? ReviewNotes) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record FollowUpQuoteEvidenceRequestCommand(
    Guid QuoteId,
    Guid EvidenceRequestId) : IRequest<QuoteEvidenceRequestResult?>;

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
    DateTime UpdatedAtUtc,
    IReadOnlyCollection<QuoteEvidenceDocumentResult> Documents);

public sealed record QuoteEvidenceDocumentResult(
    Guid DocumentId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string UploadedByUserId,
    DateTime UploadedAtUtc);

public sealed record EvidenceDocumentUpload(
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    Stream Content);

public sealed record EvidenceDocumentDownloadResult(
    string OriginalFileName,
    string ContentType,
    Stream Content);

public sealed class CreateQuoteEvidenceRequestCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<CreateQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        CreateQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var quote = await quoteRepository.GetForUnderwritingReviewAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return null;

        if (quote.Status != QuoteStatus.Referred)
            throw new InvalidOperationException("Evidence can only be requested while a quote is referred.");

        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(request.QuoteId, cancellationToken)
            ?? throw new InvalidOperationException("Referral operations must exist before evidence can be requested.");

        var requestedAtUtc = DateTime.UtcNow;
        var evidenceRequest = QuoteEvidenceRequest.Create(
            quote.Id,
            quote.SubmissionId,
            operation.Id,
            quote.OwnerUserId,
            CurrentUserId.GetRequired(currentUser, "An authenticated underwriter user id is required to request evidence."),
            request.Category,
            request.Title,
            request.Description,
            request.DueAtUtc,
            requestedAtUtc);

        await quoteRepository.AddEvidenceRequestAsync(evidenceRequest, cancellationToken);
        operation.RecordEvidenceRequestCreated(
            evidenceRequest.Id,
            evidenceRequest.RequestedByUserId,
            requestedAtUtc);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest);
    }
}

public sealed class RespondToQuoteEvidenceRequestCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDocumentStorageService documentStorageService)
    : IRequestHandler<RespondToQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
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

    public async Task<QuoteEvidenceRequestResult?> Handle(
        RespondToQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        ValidateDocumentUploads(request.Documents);

        var ownerUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated owner user id is required to respond to evidence requests.");
        var evidenceRequest = await quoteRepository.GetEvidenceRequestForOwnerAsync(
            request.EvidenceRequestId,
            ownerUserId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(
            evidenceRequest.QuoteId,
            cancellationToken)
            ?? throw new InvalidOperationException("Referral operations must exist before evidence can be updated.");
        var respondedAtUtc = DateTime.UtcNow;
        var evidenceDocuments = new List<QuoteEvidenceDocument>();

        foreach (var document in request.Documents)
        {
            var storedDocument = await documentStorageService.StoreAsync(
                new DocumentStorageUpload(
                    document.OriginalFileName,
                    document.ContentType,
                    document.Content),
                cancellationToken);

            evidenceDocuments.Add(QuoteEvidenceDocument.Create(
                evidenceRequest.Id,
                evidenceRequest.QuoteId,
                evidenceRequest.SubmissionId,
                evidenceRequest.OwnerUserId,
                Path.GetFileName(document.OriginalFileName),
                document.ContentType,
                document.SizeBytes,
                storedDocument.StorageKey,
                ownerUserId,
                respondedAtUtc));
        }

        evidenceRequest.Respond(
            ownerUserId,
            request.RespondentName,
            request.RespondentTitle,
            request.ResponseText,
            evidenceDocuments.FirstOrDefault()?.OriginalFileName ?? request.AttachmentFileName,
            evidenceDocuments.FirstOrDefault()?.ContentType ?? request.AttachmentContentType,
            evidenceDocuments.FirstOrDefault()?.SizeBytes ?? request.AttachmentSizeBytes,
            respondedAtUtc);
        operation.RecordEvidenceRequestResponded(
            evidenceRequest.Id,
            ownerUserId,
            respondedAtUtc);

        if (evidenceDocuments.Count > 0)
            await quoteRepository.AddEvidenceDocumentsAsync(evidenceDocuments, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest, evidenceDocuments);
    }

    private static void ValidateDocumentUploads(IReadOnlyCollection<EvidenceDocumentUpload> documents)
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
}

public sealed class AcceptQuoteEvidenceRequestCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<AcceptQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        AcceptQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await quoteRepository.GetEvidenceRequestForUnderwritingAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(
            request.QuoteId,
            cancellationToken)
            ?? throw new InvalidOperationException("Referral operations must exist before evidence can be reviewed.");
        var underwriterUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated underwriter user id is required to accept evidence.");
        var acceptedAtUtc = DateTime.UtcNow;

        evidenceRequest.Accept(underwriterUserId, request.ReviewNotes, acceptedAtUtc);
        operation.RecordEvidenceRequestAccepted(evidenceRequest.Id, underwriterUserId, acceptedAtUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest);
    }
}

public sealed class CancelQuoteEvidenceRequestCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<CancelQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        CancelQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await quoteRepository.GetEvidenceRequestForUnderwritingAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(
            request.QuoteId,
            cancellationToken)
            ?? throw new InvalidOperationException("Referral operations must exist before evidence can be reviewed.");
        var underwriterUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated underwriter user id is required to cancel evidence.");
        var cancelledAtUtc = DateTime.UtcNow;

        evidenceRequest.Cancel(underwriterUserId, request.ReviewNotes, cancelledAtUtc);
        operation.RecordEvidenceRequestCancelled(evidenceRequest.Id, underwriterUserId, cancelledAtUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest);
    }
}

public sealed class FollowUpQuoteEvidenceRequestCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<FollowUpQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        FollowUpQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await quoteRepository.GetEvidenceRequestForUnderwritingAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(
            request.QuoteId,
            cancellationToken)
            ?? throw new InvalidOperationException("Referral operations must exist before evidence can receive follow-up.");
        var underwriterUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated underwriter user id is required to follow up evidence.");
        var followedUpAtUtc = DateTime.UtcNow;

        evidenceRequest.RecordFollowUpSent(underwriterUserId, followedUpAtUtc);
        operation.RecordEvidenceRequestFollowUpSent(evidenceRequest.Id, underwriterUserId, followedUpAtUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest);
    }
}

public sealed class ListOwnerEvidenceRequestsQueryHandler(
    IQuoteRepository quoteRepository,
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
        var evidenceRequests = await quoteRepository.ListEvidenceRequestsForOwnerAsync(
            ownerUserId,
            cancellationToken);
        var documents = await quoteRepository.ListEvidenceDocumentsForRequestsAsync(
            evidenceRequests.Select(evidenceRequest => evidenceRequest.Id).ToList(),
            cancellationToken);
        var documentsByRequestId = documents
            .GroupBy(document => document.EvidenceRequestId)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<QuoteEvidenceDocument>)group.ToList());

        return new ListOwnerEvidenceRequestsResult(
            evidenceRequests
                .Select(evidenceRequest => QuoteEvidenceRequestResultFactory.FromRequest(
                    evidenceRequest,
                    documentsByRequestId.GetValueOrDefault(evidenceRequest.Id) ?? []))
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
            document.UploadedAtUtc);
    }
}

internal static class EvidenceDocumentDownloadResultFactory
{
    public static async Task<EvidenceDocumentDownloadResult?> OpenDocumentAsync(
        IDocumentStorageService documentStorageService,
        QuoteEvidenceDocument document,
        CancellationToken cancellationToken)
    {
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
