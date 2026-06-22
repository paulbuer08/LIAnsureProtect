using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.UnitTests.Quotes;

public sealed class QuoteEvidenceRequestTests
{
    [Fact]
    public void Create_records_open_request_with_realistic_category_and_audit_fields()
    {
        var requestedAtUtc = new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc);
        var dueAtUtc = requestedAtUtc.AddDays(3);

        var request = QuoteEvidenceRequest.Create(
            quoteId: Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            submissionId: Guid.Parse("6d3f563f-595c-4ad6-90ef-5d7d75066763"),
            quoteReferralOperationId: Guid.Parse("51de5b32-a53d-4bba-998d-3289ba18db0a"),
            ownerUserId: "customer-1",
            requestedByUserId: "underwriter-1",
            category: EvidenceRequestCategory.MultiFactorAuthentication,
            title: "Confirm MFA rollout",
            description: "Please provide current MFA rollout evidence for privileged and email access.",
            dueAtUtc: dueAtUtc,
            requestedAtUtc: requestedAtUtc);

        Assert.Equal(EvidenceRequestStatus.Open, request.Status);
        Assert.Equal(EvidenceRequestCategory.MultiFactorAuthentication, request.Category);
        Assert.Equal("Confirm MFA rollout", request.Title);
        Assert.Equal("customer-1", request.OwnerUserId);
        Assert.Equal("underwriter-1", request.RequestedByUserId);
        Assert.Equal(requestedAtUtc, request.RequestedAtUtc);
        Assert.Equal(dueAtUtc, request.DueAtUtc);
    }

    [Fact]
    public void Respond_records_text_and_safe_attachment_metadata_without_accepting_evidence()
    {
        var request = CreateRequest();
        var respondedAtUtc = new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc);

        request.Respond(
            respondedByUserId: "customer-1",
            respondentName: "Jane Applicant",
            respondentTitle: "CISO",
            responseText: "MFA is enforced for all email and privileged accounts.",
            attachmentFileName: "mfa-attestation.pdf",
            attachmentContentType: "application/pdf",
            attachmentSizeBytes: 124_000,
            respondedAtUtc: respondedAtUtc);

        Assert.Equal(EvidenceRequestStatus.Responded, request.Status);
        Assert.Equal("customer-1", request.RespondedByUserId);
        Assert.Equal("Jane Applicant", request.RespondentName);
        Assert.Equal("CISO", request.RespondentTitle);
        Assert.Equal("MFA is enforced for all email and privileged accounts.", request.ResponseText);
        Assert.Equal("mfa-attestation.pdf", request.AttachmentFileName);
        Assert.Equal("application/pdf", request.AttachmentContentType);
        Assert.Equal(124_000, request.AttachmentSizeBytes);
        Assert.Equal(respondedAtUtc, request.RespondedAtUtc);
        Assert.Null(request.AcceptedAtUtc);
    }

    [Fact]
    public void Accept_requires_responded_request_and_records_underwriter_review()
    {
        var request = CreateRequest();
        request.Respond(
            "customer-1",
            "Jane Applicant",
            "CISO",
            "MFA rollout evidence attached.",
            "mfa-attestation.pdf",
            "application/pdf",
            124_000,
            new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc));

        var acceptedAtUtc = new DateTime(2026, 6, 22, 13, 0, 0, DateTimeKind.Utc);
        request.Accept("underwriter-1", "Evidence is sufficient for MFA review.", acceptedAtUtc);

        Assert.Equal(EvidenceRequestStatus.Accepted, request.Status);
        Assert.Equal("underwriter-1", request.AcceptedByUserId);
        Assert.Equal("Evidence is sufficient for MFA review.", request.ReviewNotes);
        Assert.Equal(acceptedAtUtc, request.AcceptedAtUtc);
    }

    [Fact]
    public void Cancel_blocks_later_response()
    {
        var request = CreateRequest();
        var cancelledAtUtc = new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc);

        request.Cancel("underwriter-1", "No longer required after underwriter review.", cancelledAtUtc);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            request.Respond(
                "customer-1",
                "Jane Applicant",
                "CISO",
                "Late response.",
                null,
                null,
                null,
                cancelledAtUtc.AddMinutes(10)));

        Assert.Equal(EvidenceRequestStatus.Cancelled, request.Status);
        Assert.Equal("underwriter-1", request.CancelledByUserId);
        Assert.Equal("Evidence request is already closed.", exception.Message);
    }

    private static QuoteEvidenceRequest CreateRequest()
    {
        var requestedAtUtc = new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc);

        return QuoteEvidenceRequest.Create(
            Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            Guid.Parse("6d3f563f-595c-4ad6-90ef-5d7d75066763"),
            Guid.Parse("51de5b32-a53d-4bba-998d-3289ba18db0a"),
            "customer-1",
            "underwriter-1",
            EvidenceRequestCategory.MultiFactorAuthentication,
            "Confirm MFA rollout",
            "Please provide current MFA rollout evidence for privileged and email access.",
            requestedAtUtc.AddDays(3),
            requestedAtUtc);
    }
}
