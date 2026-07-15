using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;

namespace LIAnsureProtect.UnitTests.Modules.Underwriting.Evidence;

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
            ownerUserId: "customer-1",
            requestedByUserId: "underwriter-1",
            category: EvidenceRequestCategory.MultiFactorAuthentication,
            title: "Confirm MFA rollout",
            description: "Please provide current MFA rollout evidence for privileged and email access.",
            dueAtUtc: dueAtUtc,
            requestedAtUtc: requestedAtUtc,
            quoteVersion: 2,
            documentRequirement: EvidenceDocumentRequirement.Required,
            submissionReference: "SUB-2026-6D3F563F595C4AD6",
            companyName: "Example Company");

        Assert.Equal(EvidenceRequestStatus.Open, request.Status);
        Assert.Equal(EvidenceReviewDecisionStatus.NotReviewed, request.ReviewDecision);
        Assert.Null(request.ReviewReason);
        Assert.Null(request.RemediationGuidance);
        Assert.Null(request.ReviewedByUserId);
        Assert.Null(request.ReviewedAtUtc);
        Assert.Equal(EvidenceRequestCategory.MultiFactorAuthentication, request.Category);
        Assert.Equal(EvidenceDocumentRequirement.Required, request.DocumentRequirement);
        Assert.Equal("SUB-2026-6D3F563F595C4AD6", request.SubmissionReference);
        Assert.Equal("Example Company", request.CompanyName);
        Assert.Equal("Confirm MFA rollout", request.Title);
        Assert.Equal("customer-1", request.OwnerUserId);
        Assert.Equal("underwriter-1", request.RequestedByUserId);
        Assert.Equal(requestedAtUtc, request.RequestedAtUtc);
        Assert.Equal(dueAtUtc, request.DueAtUtc);

        var domainEvent = Assert.IsType<QuoteEvidenceRequestCreatedDomainEvent>(
            Assert.Single(request.DomainEvents));
        Assert.Equal(request.Id, domainEvent.EvidenceRequestId);
        Assert.Equal(request.QuoteId, domainEvent.QuoteId);
        Assert.Equal(request.SubmissionId, domainEvent.SubmissionId);
        Assert.Equal("customer-1", domainEvent.OwnerUserId);
        Assert.Equal("underwriter-1", domainEvent.RequestedByUserId);
        Assert.Equal(EvidenceRequestCategory.MultiFactorAuthentication, domainEvent.Category);
        Assert.Equal(dueAtUtc, domainEvent.DueAtUtc);
        Assert.Equal("Confirm MFA rollout", domainEvent.Title);
        Assert.Equal(2, domainEvent.QuoteVersion);
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
            respondentEmail: "jane@example.com",
            respondentPhone: null,
            responseText: "MFA is enforced for all email and privileged accounts.",
            otherConcerns: null,
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
        Assert.Equal(EvidenceReviewDecisionStatus.NotReviewed, request.ReviewDecision);

        var domainEvent = Assert.IsType<QuoteEvidenceRequestRespondedDomainEvent>(
            request.DomainEvents.Last());
        Assert.Equal(request.Id, domainEvent.EvidenceRequestId);
        Assert.Equal("customer-1", domainEvent.OwnerUserId);
        Assert.Equal("underwriter-1", domainEvent.RequestedByUserId);
        Assert.Equal("customer-1", domainEvent.RespondedByUserId);
    }

    [Fact]
    public void Accept_requires_responded_request_and_records_underwriter_review()
    {
        var request = CreateRequest();
        request.Respond(
            "customer-1",
            "Jane Applicant",
            "CISO",
            "jane@example.com",
            null,
            "MFA rollout evidence attached.",
            null,
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
        Assert.Equal(EvidenceReviewDecisionStatus.Satisfied, request.ReviewDecision);
        Assert.Equal("Evidence is sufficient for MFA review.", request.ReviewReason);
        Assert.Null(request.RemediationGuidance);
        Assert.Equal("underwriter-1", request.ReviewedByUserId);
        Assert.Equal(acceptedAtUtc, request.ReviewedAtUtc);

        var domainEvent = Assert.IsType<QuoteEvidenceRequestAcceptedDomainEvent>(
            request.DomainEvents.Last());
        Assert.Equal(request.Id, domainEvent.EvidenceRequestId);
        Assert.Equal("customer-1", domainEvent.OwnerUserId);
        Assert.Equal("underwriter-1", domainEvent.AcceptedByUserId);
        Assert.DoesNotContain(
            request.DomainEvents,
            domainEvent => domainEvent.GetType().Name == "QuoteEvidenceRequestRemediationRequiredDomainEvent");
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
                "jane@example.com",
                null,
                "Late response.",
                null,
                null,
                null,
                null,
                cancelledAtUtc.AddMinutes(10)));

        Assert.Equal(EvidenceRequestStatus.Cancelled, request.Status);
        Assert.Equal("underwriter-1", request.CancelledByUserId);
        Assert.Equal("Evidence request is already closed.", exception.Message);

        var domainEvent = Assert.IsType<QuoteEvidenceRequestCancelledDomainEvent>(
            request.DomainEvents.Last());
        Assert.Equal(request.Id, domainEvent.EvidenceRequestId);
        Assert.Equal("customer-1", domainEvent.OwnerUserId);
        Assert.Equal("underwriter-1", domainEvent.CancelledByUserId);
    }

    [Fact]
    public void RecordReviewDecision_records_insufficient_decision_with_owner_remediation_guidance()
    {
        var request = CreateRespondedRequest();
        var reviewedAtUtc = new DateTime(2026, 6, 22, 14, 0, 0, DateTimeKind.Utc);

        request.RecordReviewDecision(
            EvidenceReviewDecisionStatus.Insufficient,
            "Screenshot only covered email MFA and did not prove privileged account MFA.",
            "Please upload privileged access MFA evidence or a signed control attestation.",
            "underwriter-1",
            reviewedAtUtc);

        Assert.Equal(EvidenceRequestStatus.Responded, request.Status);
        Assert.Equal(EvidenceReviewDecisionStatus.Insufficient, request.ReviewDecision);
        Assert.Equal("Screenshot only covered email MFA and did not prove privileged account MFA.", request.ReviewReason);
        Assert.Equal("Please upload privileged access MFA evidence or a signed control attestation.", request.RemediationGuidance);
        Assert.Equal("underwriter-1", request.ReviewedByUserId);
        Assert.Equal(reviewedAtUtc, request.ReviewedAtUtc);
        Assert.Equal(reviewedAtUtc, request.UpdatedAtUtc);

        var domainEvent = Assert.IsType<QuoteEvidenceRequestRemediationRequiredDomainEvent>(
            request.DomainEvents.Last());
        Assert.Equal(request.Id, domainEvent.EvidenceRequestId);
        Assert.Equal(EvidenceReviewDecisionStatus.Insufficient, domainEvent.Decision);
        Assert.Equal("Screenshot only covered email MFA and did not prove privileged account MFA.", domainEvent.ReviewReason);
        Assert.Equal("Please upload privileged access MFA evidence or a signed control attestation.", domainEvent.RemediationGuidance);
        Assert.Equal("underwriter-1", domainEvent.ReviewedByUserId);
    }

    [Fact]
    public void RecordReviewDecision_records_clarification_decision_with_remediation_notification_event()
    {
        var request = CreateRespondedRequest();
        var reviewedAtUtc = new DateTime(2026, 6, 22, 14, 0, 0, DateTimeKind.Utc);

        request.RecordReviewDecision(
            EvidenceReviewDecisionStatus.NeedsClarification,
            "The response did not explain whether administrator accounts are covered.",
            "Please clarify MFA scope for privileged accounts.",
            "underwriter-1",
            reviewedAtUtc);

        var domainEvent = Assert.IsType<QuoteEvidenceRequestRemediationRequiredDomainEvent>(
            request.DomainEvents.Last());
        Assert.Equal(request.Id, domainEvent.EvidenceRequestId);
        Assert.Equal(request.QuoteId, domainEvent.QuoteId);
        Assert.Equal(request.SubmissionId, domainEvent.SubmissionId);
        Assert.Equal("customer-1", domainEvent.OwnerUserId);
        Assert.Equal("underwriter-1", domainEvent.RequestedByUserId);
        Assert.Equal(EvidenceRequestCategory.MultiFactorAuthentication, domainEvent.Category);
        Assert.Equal(EvidenceReviewDecisionStatus.NeedsClarification, domainEvent.Decision);
        Assert.Equal("The response did not explain whether administrator accounts are covered.", domainEvent.ReviewReason);
        Assert.Equal("Please clarify MFA scope for privileged accounts.", domainEvent.RemediationGuidance);
        Assert.Equal("underwriter-1", domainEvent.ReviewedByUserId);
        Assert.Equal(reviewedAtUtc, domainEvent.OccurredAtUtc);
    }

    [Fact]
    public void RecordReviewDecision_requires_responded_request()
    {
        var request = CreateRequest();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            request.RecordReviewDecision(
                EvidenceReviewDecisionStatus.NeedsClarification,
                "The response needs clarification.",
                "Please explain whether MFA applies to administrators.",
                "underwriter-1",
                new DateTime(2026, 6, 22, 14, 0, 0, DateTimeKind.Utc)));

        Assert.Equal("Only responded evidence requests can receive review decisions.", exception.Message);
    }

    [Fact]
    public void RecordReviewDecision_requires_remediation_guidance_for_unfavorable_decisions()
    {
        var request = CreateRespondedRequest();

        var exception = Assert.Throws<ArgumentException>(() =>
            request.RecordReviewDecision(
                EvidenceReviewDecisionStatus.NeedsClarification,
                "The response is ambiguous.",
                null,
                "underwriter-1",
                new DateTime(2026, 6, 22, 14, 0, 0, DateTimeKind.Utc)));

        Assert.Equal("Remediation guidance is required for insufficient or clarification-needed evidence.", exception.Message);
    }

    [Fact]
    public void Supplemental_response_after_unfavorable_review_resets_current_review_state()
    {
        var request = CreateRespondedRequest();
        request.RecordReviewDecision(
            EvidenceReviewDecisionStatus.NeedsClarification,
            "The response did not explain whether admin accounts are covered.",
            "Please clarify MFA scope for privileged accounts.",
            "underwriter-1",
            new DateTime(2026, 6, 22, 14, 0, 0, DateTimeKind.Utc));

        request.Respond(
            "customer-1",
            "Jane Applicant",
            "CISO",
            "jane@example.com",
            null,
            "Supplemental response: MFA is enforced for email and all privileged accounts.",
            null,
            null,
            null,
            null,
            new DateTime(2026, 6, 22, 15, 0, 0, DateTimeKind.Utc));

        Assert.Equal(EvidenceRequestStatus.Responded, request.Status);
        Assert.Equal(EvidenceReviewDecisionStatus.NotReviewed, request.ReviewDecision);
        Assert.Null(request.ReviewReason);
        Assert.Null(request.RemediationGuidance);
        Assert.Null(request.ReviewedByUserId);
        Assert.Null(request.ReviewedAtUtc);
        Assert.Equal("Supplemental response: MFA is enforced for email and all privileged accounts.", request.ResponseText);
    }

    [Fact]
    public void Pre_review_follow_up_adds_concerns_without_overwriting_the_original_response()
    {
        var request = CreateRespondedRequest();
        var originalResponse = request.ResponseText;

        request.RecordSupplementalResponse(
            "customer-1",
            "Jane Applicant",
            "CISO",
            "jane@example.com",
            "+63 917 555 0101",
            null,
            "The privileged-account export will be available tomorrow.",
            null,
            null,
            null,
            new DateTime(2026, 6, 22, 13, 0, 0, DateTimeKind.Utc));

        Assert.Equal(EvidenceRequestStatus.Responded, request.Status);
        Assert.Equal(EvidenceReviewDecisionStatus.NotReviewed, request.ReviewDecision);
        Assert.Equal(originalResponse, request.ResponseText);
        Assert.Equal("The privileged-account export will be available tomorrow.", request.OtherConcerns);
        Assert.Equal("+639175550101", request.RespondentMobileNumber);
        Assert.IsType<QuoteEvidenceRequestRespondedDomainEvent>(request.DomainEvents.Last());
    }

    [Fact]
    public void Pre_review_follow_up_requires_meaningful_content_and_valid_contact_email()
    {
        var request = CreateRespondedRequest();

        var emptyException = Assert.Throws<ArgumentException>(() =>
            request.RecordSupplementalResponse(
                "customer-1", "Jane Applicant", "CISO", "jane@example.com", null,
                null, null, null, null, null,
                new DateTime(2026, 6, 22, 13, 0, 0, DateTimeKind.Utc)));
        var emailException = Assert.Throws<ArgumentException>(() =>
            request.RecordSupplementalResponse(
                "customer-1", "Jane Applicant", "CISO", "not-an-email", null,
                "Additional context.", null, null, null, null,
                new DateTime(2026, 6, 22, 13, 0, 0, DateTimeKind.Utc)));

        Assert.Equal("A supplemental response requires changed contact details, a message, a concern, or a supporting document.", emptyException.Message);
        Assert.Equal("Respondent email must be a valid email address. (Parameter 'value')", emailException.Message);
    }

    [Fact]
    public void Pre_review_follow_up_accepts_a_changed_mobile_number_by_itself()
    {
        var request = CreateRespondedRequest();

        request.RecordSupplementalResponse(
            "customer-1",
            "Jane Applicant",
            "CISO",
            "jane@example.com",
            "09171234567",
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            new DateTime(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc));

        Assert.Equal("+639171234567", request.RespondentMobileNumber);
        Assert.Null(request.RespondentTelephoneNumber);
    }

    [Fact]
    public void Pre_review_follow_up_blocks_a_sixth_currently_unread_entry()
    {
        var request = CreateRespondedRequest();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            request.RecordSupplementalResponse(
                "customer-1",
                "Jane Applicant",
                "CISO",
                "jane@example.com",
                null,
                null,
                "Another follow-up.",
                null,
                null,
                null,
                null,
                EvidenceResponseFieldRules.MaxPendingFollowUps,
                new DateTime(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc)));

        Assert.Contains("Up to 5 unread follow-ups are allowed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecordFollowUpSent_requires_open_request_and_records_notification_event()
    {
        var request = CreateRequest();
        var followedUpAtUtc = new DateTime(2026, 6, 26, 9, 0, 0, DateTimeKind.Utc);

        request.RecordFollowUpSent("underwriter-1", followedUpAtUtc);

        var domainEvent = Assert.IsType<QuoteEvidenceRequestFollowUpSentDomainEvent>(
            request.DomainEvents.Last());
        Assert.Equal(request.Id, domainEvent.EvidenceRequestId);
        Assert.Equal("customer-1", domainEvent.OwnerUserId);
        Assert.Equal("underwriter-1", domainEvent.FollowedUpByUserId);
        Assert.Equal(followedUpAtUtc, request.UpdatedAtUtc);
    }

    [Fact]
    public void RecordFollowUpSent_blocks_closed_request()
    {
        var request = CreateRequest();
        request.Cancel(
            "underwriter-1",
            "No longer needed.",
            new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            request.RecordFollowUpSent(
                "underwriter-1",
                new DateTime(2026, 6, 26, 9, 0, 0, DateTimeKind.Utc)));

        Assert.Equal("Only open evidence requests can receive follow-up reminders.", exception.Message);
    }

    private static QuoteEvidenceRequest CreateRespondedRequest()
    {
        var request = CreateRequest();
        request.Respond(
            "customer-1",
            "Jane Applicant",
            "CISO",
            "jane@example.com",
            null,
            "MFA rollout evidence attached.",
            null,
            "mfa-attestation.pdf",
            "application/pdf",
            124_000,
            new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc));

        return request;
    }

    private static QuoteEvidenceRequest CreateRequest()
    {
        var requestedAtUtc = new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc);

        return QuoteEvidenceRequest.Create(
            Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            Guid.Parse("6d3f563f-595c-4ad6-90ef-5d7d75066763"),
            "customer-1",
            "underwriter-1",
            EvidenceRequestCategory.MultiFactorAuthentication,
            "Confirm MFA rollout",
            "Please provide current MFA rollout evidence for privileged and email access.",
            requestedAtUtc.AddDays(3),
            requestedAtUtc);
    }
}
