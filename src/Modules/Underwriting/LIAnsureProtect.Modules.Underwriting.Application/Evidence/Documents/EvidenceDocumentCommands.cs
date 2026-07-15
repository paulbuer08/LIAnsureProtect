using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence.Documents;
using LIAnsureProtect.Platform.Abstractions.Documents;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;

public sealed record RespondToQuoteEvidenceRequestCommand(
    Guid EvidenceRequestId,
    string RespondentName,
    string RespondentTitle,
    string RespondentEmail,
    string? RespondentMobileNumber,
    string? RespondentTelephoneNumber,
    string? ResponseText,
    string? OtherConcerns,
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

public sealed record MarkQuoteEvidenceFollowUpViewedCommand(
    Guid QuoteId,
    Guid EvidenceRequestId,
    Guid ResponseId) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record DownloadOwnerEvidenceDocumentQuery(
    Guid EvidenceRequestId,
    Guid DocumentId) : IRequest<EvidenceDocumentDownloadResult?>;

public sealed record DownloadUnderwritingEvidenceDocumentQuery(
    Guid QuoteId,
    Guid EvidenceRequestId,
    Guid DocumentId) : IRequest<EvidenceDocumentDownloadResult?>;

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
    IEvidenceRequestRepository evidenceRequestRepository,
    IEvidenceDocumentRepository evidenceDocumentRepository,
    ICurrentUser currentUser,
    IDocumentStorageService documentStorageService,
    IEvidenceDocumentScanner evidenceDocumentScanner)
    : IRequestHandler<RespondToQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        RespondToQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        EvidenceDocumentUploadWorkflow.ValidateDocumentUploads(request.Documents);

        var ownerUserId = CurrentEvidenceUser.GetRequiredUserId(
            currentUser,
            "An authenticated owner user id is required to respond to evidence requests.");
        var evidenceRequest = await evidenceRequestRepository.GetForOwnerAsync(
            request.EvidenceRequestId,
            ownerUserId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var isPreReviewFollowUp = evidenceRequest.Status == EvidenceRequestStatus.Responded
            && evidenceRequest.ReviewDecision == EvidenceReviewDecisionStatus.NotReviewed;
        var responseKind = isPreReviewFollowUp
            ? EvidenceResponseKind.FollowUp
            : evidenceRequest.Status == EvidenceRequestStatus.Responded
                ? EvidenceResponseKind.Remediation
                : EvidenceResponseKind.Initial;

        var pendingFollowUpCount = isPreReviewFollowUp
            ? await evidenceRequestRepository.CountPendingFollowUpsAsync(
                evidenceRequest.Id,
                cancellationToken)
            : 0;

        if (isPreReviewFollowUp
            && string.IsNullOrWhiteSpace(request.ResponseText)
            && string.IsNullOrWhiteSpace(request.OtherConcerns)
            && string.Equals(request.RespondentName.Trim(), evidenceRequest.RespondentName, StringComparison.Ordinal)
            && string.Equals(request.RespondentTitle.Trim(), evidenceRequest.RespondentTitle, StringComparison.Ordinal)
            && string.Equals(request.RespondentEmail.Trim(), evidenceRequest.RespondentEmail, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                EvidenceResponseFieldRules.PhilippineMobileNumber(request.RespondentMobileNumber),
                evidenceRequest.RespondentMobileNumber,
                StringComparison.Ordinal)
            && string.Equals(
                EvidenceResponseFieldRules.PhilippineTelephoneNumber(request.RespondentTelephoneNumber),
                evidenceRequest.RespondentTelephoneNumber,
                StringComparison.Ordinal)
            && request.Documents.Count == 0)
        {
            throw new ArgumentException(
                "A follow-up must include changed contact details, an additional response, other concerns, or at least one document.",
                nameof(request));
        }

        if (isPreReviewFollowUp
            && pendingFollowUpCount >= EvidenceResponseFieldRules.MaxPendingFollowUps)
        {
            throw new InvalidOperationException(
                $"Up to {EvidenceResponseFieldRules.MaxPendingFollowUps} unread follow-ups are allowed. Wait until underwriting opens one before sending another.");
        }

        if (evidenceRequest.DocumentRequirement == EvidenceDocumentRequirement.Required
            && !isPreReviewFollowUp
            && request.Documents.Count == 0)
        {
            throw new ArgumentException(
                "At least one supporting document is required for this evidence request.",
                nameof(request));
        }

        if (evidenceRequest.DocumentRequirement == EvidenceDocumentRequirement.NarrativeOnly
            && request.Documents.Count > 0)
        {
            throw new ArgumentException(
                "This evidence request accepts a written response only and does not accept documents.",
                nameof(request));
        }

        var respondedAtUtc = DateTime.UtcNow;
        var response = QuoteEvidenceResponse.Create(
            evidenceRequest,
            ownerUserId,
            request.RespondentName,
            request.RespondentTitle,
            request.RespondentEmail,
            request.RespondentMobileNumber,
            request.RespondentTelephoneNumber,
            request.ResponseText,
            request.OtherConcerns,
            responseKind,
            respondedAtUtc);
        var evidenceDocuments = await EvidenceDocumentUploadWorkflow.StoreAndScanDocumentsAsync(
            request.Documents,
            EvidenceDocumentRequestFacts.FromRequest(evidenceRequest, response.Id),
            ownerUserId,
            respondedAtUtc,
            documentStorageService,
            evidenceDocumentScanner,
            cancellationToken);

        if (evidenceDocuments.Count > 0)
        {
            await evidenceDocumentRepository.AddDocumentsAsync(evidenceDocuments, cancellationToken);
        }

        if (isPreReviewFollowUp)
        {
            evidenceRequest.RecordSupplementalResponse(
                ownerUserId,
                request.RespondentName,
                request.RespondentTitle,
                request.RespondentEmail,
                request.RespondentMobileNumber,
                request.RespondentTelephoneNumber,
                request.ResponseText,
                request.OtherConcerns,
                evidenceDocuments.FirstOrDefault()?.OriginalFileName ?? request.AttachmentFileName,
                evidenceDocuments.FirstOrDefault()?.ContentType ?? request.AttachmentContentType,
                evidenceDocuments.FirstOrDefault()?.SizeBytes ?? request.AttachmentSizeBytes,
                pendingFollowUpCount,
                respondedAtUtc);
        }
        else
        {
            evidenceRequest.Respond(
                ownerUserId,
                request.RespondentName,
                request.RespondentTitle,
                request.RespondentEmail,
                request.RespondentMobileNumber,
                request.RespondentTelephoneNumber,
                request.ResponseText ?? string.Empty,
                request.OtherConcerns,
                evidenceDocuments.FirstOrDefault()?.OriginalFileName ?? request.AttachmentFileName,
                evidenceDocuments.FirstOrDefault()?.ContentType ?? request.AttachmentContentType,
                evidenceDocuments.FirstOrDefault()?.SizeBytes ?? request.AttachmentSizeBytes,
                respondedAtUtc);
        }

        await evidenceRequestRepository.AddResponseAsync(response, cancellationToken);

        await evidenceRequestRepository.SaveChangesAsync(cancellationToken);

        var responseHistory = await evidenceRequestRepository.ListResponsesAsync(
            evidenceRequest.Id,
            cancellationToken);
        var documentHistory = await evidenceDocumentRepository.ListForRequestsAsync(
            [evidenceRequest.Id],
            cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(
            evidenceRequest,
            documentHistory,
            responseHistory);
    }
}

public sealed class UploadReplacementEvidenceDocumentsCommandHandler(
    IEvidenceRequestRepository evidenceRequestRepository,
    IEvidenceDocumentRepository evidenceDocumentRepository,
    ICurrentUser currentUser,
    IDocumentStorageService documentStorageService,
    IEvidenceDocumentScanner evidenceDocumentScanner)
    : IRequestHandler<UploadReplacementEvidenceDocumentsCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        UploadReplacementEvidenceDocumentsCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Documents.Count == 0)
            throw new ArgumentException("Replacement evidence uploads must include at least one file.", nameof(request));

        EvidenceDocumentUploadWorkflow.ValidateDocumentUploads(request.Documents);

        var ownerUserId = CurrentEvidenceUser.GetRequiredUserId(
            currentUser,
            "An authenticated owner user id is required to upload replacement evidence documents.");
        var evidenceRequest = await evidenceRequestRepository.GetForOwnerAsync(
            request.EvidenceRequestId,
            ownerUserId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        if (evidenceRequest.Status != EvidenceRequestStatus.Responded)
            throw new InvalidOperationException("Replacement evidence documents can only be uploaded after an evidence response.");

        var existingDocuments = await evidenceDocumentRepository.ListForRequestsAsync(
            [evidenceRequest.Id],
            cancellationToken);
        if (!existingDocuments.Any(document => document.ScanStatus is EvidenceDocumentScanStatus.Rejected or EvidenceDocumentScanStatus.Failed))
            throw new InvalidOperationException("Replacement evidence documents are only allowed after a rejected or failed security scan.");

        var uploadedAtUtc = DateTime.UtcNow;
        var replacementDocuments = await EvidenceDocumentUploadWorkflow.StoreAndScanDocumentsAsync(
            request.Documents,
            EvidenceDocumentRequestFacts.FromRequest(evidenceRequest),
            ownerUserId,
            uploadedAtUtc,
            documentStorageService,
            evidenceDocumentScanner,
            cancellationToken);

        if (replacementDocuments.Count > 0)
        {
            await evidenceDocumentRepository.AddDocumentsAsync(replacementDocuments, cancellationToken);
        }

        evidenceRequest.RecordReplacementDocumentsUploaded(
            ownerUserId,
            replacementDocuments.FirstOrDefault()?.OriginalFileName ?? evidenceRequest.AttachmentFileName,
            replacementDocuments.FirstOrDefault()?.ContentType ?? evidenceRequest.AttachmentContentType,
            replacementDocuments.FirstOrDefault()?.SizeBytes ?? evidenceRequest.AttachmentSizeBytes,
            uploadedAtUtc);

        await evidenceRequestRepository.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(
            evidenceRequest,
            existingDocuments.Concat(replacementDocuments).ToList());
    }
}

public sealed class MarkQuoteEvidenceFollowUpViewedCommandHandler(
    IEvidenceRequestRepository evidenceRequestRepository,
    IEvidenceDocumentRepository evidenceDocumentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<MarkQuoteEvidenceFollowUpViewedCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        MarkQuoteEvidenceFollowUpViewedCommand request,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await evidenceRequestRepository.GetForUnderwritingAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var response = await evidenceRequestRepository.GetResponseForUnderwritingAsync(
            request.EvidenceRequestId,
            request.ResponseId,
            cancellationToken);
        if (response is null)
            return null;

        var underwriterUserId = CurrentEvidenceUser.GetRequiredUserId(
            currentUser,
            "An authenticated underwriter user id is required to open an evidence follow-up.");
        var viewedAtUtc = DateTime.UtcNow;
        if (response.MarkViewed(underwriterUserId, viewedAtUtc))
        {
            evidenceRequest.RecordCustomerFollowUpViewed(viewedAtUtc);
            await evidenceRequestRepository.SaveChangesAsync(cancellationToken);
        }

        var responses = await evidenceRequestRepository.ListResponsesAsync(
            evidenceRequest.Id,
            cancellationToken);
        var documents = await evidenceDocumentRepository.ListForRequestsAsync(
            [evidenceRequest.Id],
            cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(
            evidenceRequest,
            documents,
            responses);
    }
}

public sealed class AcceptQuoteEvidenceRequestCommandHandler(
    IEvidenceRequestRepository evidenceRequestRepository,
    IEvidenceDocumentRepository evidenceDocumentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<AcceptQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        AcceptQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await evidenceRequestRepository.GetForUnderwritingAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var documents = await evidenceDocumentRepository.ListForRequestsAsync(
            [evidenceRequest.Id],
            cancellationToken);
        EvidenceReviewDocumentGate.EnsureReviewDocumentsAreTrusted(
            documents,
            "Only clean evidence documents can be accepted.");

        var underwriterUserId = CurrentEvidenceUser.GetRequiredUserId(
            currentUser,
            "An authenticated underwriter user id is required to accept evidence.");
        var acceptedAtUtc = DateTime.UtcNow;

        evidenceRequest.Accept(underwriterUserId, request.ReviewNotes, acceptedAtUtc);
        var review = QuoteEvidenceRequestReview.Record(
            evidenceRequest,
            EvidenceReviewDecisionStatus.Satisfied,
            request.ReviewNotes ?? "Evidence accepted by underwriting.",
            null,
            underwriterUserId,
            acceptedAtUtc,
            documents.Count,
            documents.Count(document => document.ScanStatus == EvidenceDocumentScanStatus.Clean));
        await evidenceRequestRepository.AddReviewAsync(review, cancellationToken);
        await evidenceRequestRepository.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest, documents);
    }
}

public sealed class RecordQuoteEvidenceReviewDecisionCommandHandler(
    IEvidenceRequestRepository evidenceRequestRepository,
    IEvidenceDocumentRepository evidenceDocumentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<RecordQuoteEvidenceReviewDecisionCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        RecordQuoteEvidenceReviewDecisionCommand request,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await evidenceRequestRepository.GetForUnderwritingAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var documents = await evidenceDocumentRepository.ListForRequestsAsync(
            [evidenceRequest.Id],
            cancellationToken);
        EvidenceReviewDocumentGate.EnsureReviewDocumentsAreTrusted(
            documents,
            "Only clean evidence documents can support a review decision.");

        var underwriterUserId = CurrentEvidenceUser.GetRequiredUserId(
            currentUser,
            "An authenticated underwriter user id is required to review evidence.");
        var reviewedAtUtc = DateTime.UtcNow;

        if (string.Equals(request.Decision, EvidenceReviewDecisionStatus.Satisfied.ToString(), StringComparison.Ordinal))
        {
            evidenceRequest.Accept(underwriterUserId, request.Reason, reviewedAtUtc);
            var satisfiedReview = QuoteEvidenceRequestReview.Record(
                evidenceRequest,
                EvidenceReviewDecisionStatus.Satisfied,
                request.Reason,
                null,
                underwriterUserId,
                reviewedAtUtc,
                documents.Count,
                documents.Count(document => document.ScanStatus == EvidenceDocumentScanStatus.Clean));
            await evidenceRequestRepository.AddReviewAsync(satisfiedReview, cancellationToken);
        }
        else
        {
            if (!Enum.TryParse<EvidenceReviewDecisionStatus>(request.Decision, ignoreCase: false, out var parsedDecision))
                throw new ArgumentException("Evidence review decision is not supported.", nameof(request));

            evidenceRequest.RecordReviewDecision(
                parsedDecision,
                request.Reason,
                request.RemediationGuidance,
                underwriterUserId,
                reviewedAtUtc);
            var review = QuoteEvidenceRequestReview.Record(
                evidenceRequest,
                parsedDecision,
                request.Reason,
                request.RemediationGuidance,
                underwriterUserId,
                reviewedAtUtc,
                documents.Count,
                documents.Count(document => document.ScanStatus == EvidenceDocumentScanStatus.Clean));
            await evidenceRequestRepository.AddReviewAsync(review, cancellationToken);
        }

        await evidenceRequestRepository.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest, documents);
    }
}

public sealed class DownloadOwnerEvidenceDocumentQueryHandler(
    IEvidenceDocumentRepository evidenceDocumentRepository,
    ICurrentUser currentUser,
    IDocumentStorageService documentStorageService)
    : IRequestHandler<DownloadOwnerEvidenceDocumentQuery, EvidenceDocumentDownloadResult?>
{
    public async Task<EvidenceDocumentDownloadResult?> Handle(
        DownloadOwnerEvidenceDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = CurrentEvidenceUser.GetRequiredUserId(
            currentUser,
            "An authenticated owner user id is required to download evidence documents.");
        var document = await evidenceDocumentRepository.GetForOwnerAsync(
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
    IEvidenceDocumentRepository evidenceDocumentRepository,
    IDocumentStorageService documentStorageService)
    : IRequestHandler<DownloadUnderwritingEvidenceDocumentQuery, EvidenceDocumentDownloadResult?>
{
    public async Task<EvidenceDocumentDownloadResult?> Handle(
        DownloadUnderwritingEvidenceDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var document = await evidenceDocumentRepository.GetForUnderwritingAsync(
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

internal sealed record EvidenceDocumentRequestFacts(
    Guid EvidenceRequestId,
    Guid? EvidenceResponseId,
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    string Category)
{
    public static EvidenceDocumentRequestFacts FromRequest(
        QuoteEvidenceRequest request,
        Guid? evidenceResponseId = null)
    {
        return new EvidenceDocumentRequestFacts(
            request.Id,
            evidenceResponseId,
            request.QuoteId,
            request.SubmissionId,
            request.OwnerUserId,
            request.Category.ToString());
    }
}

internal static class EvidenceDocumentUploadWorkflow
{
    public static void ValidateDocumentUploads(IReadOnlyCollection<EvidenceDocumentUpload> documents)
    {
        if (documents.Count > EvidenceDocumentUploadRules.MaximumDocumentCount)
            throw new ArgumentException("Evidence responses can include up to 5 files.", nameof(documents));

        if (documents.Sum(document => document.SizeBytes) > EvidenceDocumentUploadRules.MaximumTotalDocumentSizeBytes)
            throw new ArgumentException("Evidence response documents cannot exceed 50 MB in total.", nameof(documents));

        foreach (var document in documents)
        {
            if (document.SizeBytes <= 0)
                throw new ArgumentException("Evidence documents cannot be empty.", nameof(documents));

            if (document.SizeBytes > EvidenceDocumentUploadRules.MaximumDocumentSizeBytes)
                throw new ArgumentException("Each evidence document must be 10 MB or smaller.", nameof(documents));

            var fileName = Path.GetFileName(document.OriginalFileName);
            if (string.IsNullOrWhiteSpace(fileName) || fileName != document.OriginalFileName)
                throw new ArgumentException("Evidence document file names must not contain path information.", nameof(documents));

            if (!EvidenceDocumentUploadRules.AllowedExtensionsByContentType.TryGetValue(document.ContentType, out var expectedExtension))
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
                uploadedAtUtc,
                evidenceRequest.EvidenceResponseId);

            var storedDownload = await documentStorageService.OpenReadAsync(storedDocument.StorageKey, cancellationToken)
                ?? throw new InvalidOperationException("Stored evidence document could not be opened for security screening.");
            await using (storedDownload.Content)
            {
                var scanResult = await evidenceDocumentScanner.ScanAsync(
                    new EvidenceDocumentScanRequest(
                        evidenceDocument.OriginalFileName,
                        evidenceDocument.ContentType,
                        evidenceDocument.SizeBytes,
                        evidenceRequest.Category,
                        storedDownload.Content),
                    cancellationToken);

                evidenceDocument.RecordScanResult(
                    scanResult.ScanStatus,
                    scanResult.ScannerProviderName,
                    scanResult.ScanResultCode,
                    scanResult.ScanResultReason,
                    scanResult.Sha256,
                    scanResult.ScannedAtUtc,
                    scanResult.AssessmentVersion,
                    scanResult.PlausibilityStatus,
                    scanResult.ClaimConsistencyStatus,
                    System.Text.Json.JsonSerializer.Serialize(scanResult.AdvisoryFindings));
            }

            evidenceDocuments.Add(evidenceDocument);
        }

        return evidenceDocuments;
    }
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
