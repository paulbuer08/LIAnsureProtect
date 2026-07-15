using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;

namespace LIAnsureProtect.UnitTests.Modules.Underwriting.Evidence;

public sealed class QuoteEvidenceResponseTests
{
    [Fact]
    public void Create_preserves_trimmed_contact_and_follow_up_concerns_as_an_immutable_audit_entry()
    {
        var request = CreateRequest();

        var response = QuoteEvidenceResponse.Create(
            request,
            "customer-1",
            " Jane Applicant ",
            " CISO ",
            "jane@example.com",
            " +63 917 555 0101 ",
            "02 8123 4567",
            null,
            " Export follows tomorrow. ",
            EvidenceResponseKind.FollowUp,
            new DateTime(2026, 6, 22, 13, 0, 0, DateTimeKind.Utc));

        Assert.Equal(request.Id, response.EvidenceRequestId);
        Assert.Equal("Jane Applicant", response.RespondentName);
        Assert.Equal("CISO", response.RespondentTitle);
        Assert.Equal("jane@example.com", response.RespondentEmail);
        Assert.Equal("+639175550101", response.RespondentMobileNumber);
        Assert.Equal("+63281234567", response.RespondentTelephoneNumber);
        Assert.Null(response.ResponseText);
        Assert.Equal("Export follows tomorrow.", response.OtherConcerns);
        Assert.Equal(EvidenceResponseKind.FollowUp, response.Kind);
    }

    [Fact]
    public void Create_rejects_non_philippine_contact_formats_and_excessive_text()
    {
        var request = CreateRequest();

        Assert.Throws<ArgumentException>(() => QuoteEvidenceResponse.Create(
            request, "customer-1", "Jane", "CISO", "jane@example.com",
            "+1 202 555 0101", null, "Additional context.", null,
            EvidenceResponseKind.FollowUp, DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => QuoteEvidenceResponse.Create(
            request, "customer-1", "Jane", "CISO", "jane@example.com",
            null, null, new string('x', EvidenceResponseFieldRules.ResponseTextMaxLength + 1), null,
            EvidenceResponseKind.FollowUp, DateTime.UtcNow));
    }

    [Fact]
    public void MarkViewed_is_idempotent_and_only_applies_to_customer_follow_ups()
    {
        var request = CreateRequest();
        var response = QuoteEvidenceResponse.Create(
            request, "customer-1", "Jane", "CISO", "jane@example.com",
            null, null, "Additional context.", null,
            EvidenceResponseKind.FollowUp, DateTime.UtcNow);
        var viewedAtUtc = new DateTime(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc);

        Assert.True(response.MarkViewed("underwriter-1", viewedAtUtc));
        Assert.False(response.MarkViewed("underwriter-2", viewedAtUtc.AddMinutes(1)));
        Assert.Equal("underwriter-1", response.ViewedByUserId);
        Assert.Equal(viewedAtUtc, response.ViewedAtUtc);
    }

    [Fact]
    public void Create_rejects_invalid_email_and_missing_initial_narrative()
    {
        var request = CreateRequest();

        Assert.Throws<ArgumentException>(() => QuoteEvidenceResponse.Create(
            request, "customer-1", "Jane", "CISO", "invalid", null,
            "MFA is enabled.", null, EvidenceResponseKind.Initial, DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => QuoteEvidenceResponse.Create(
            request, "customer-1", "Jane", "CISO", "jane@example.com", null,
            null, null, EvidenceResponseKind.Initial, DateTime.UtcNow));
    }

    private static QuoteEvidenceRequest CreateRequest()
    {
        var requestedAtUtc = new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc);
        return QuoteEvidenceRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer-1",
            "underwriter-1",
            EvidenceRequestCategory.MultiFactorAuthentication,
            "Confirm MFA rollout",
            "Provide current MFA evidence.",
            requestedAtUtc.AddDays(7),
            requestedAtUtc);
    }
}
