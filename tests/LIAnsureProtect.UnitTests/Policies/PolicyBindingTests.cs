using LIAnsureProtect.Domain.Policies;
using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.UnitTests.Policies;

public sealed class PolicyBindingTests
{
    [Fact]
    public void Quoted_quote_can_be_accepted_with_subjectivity_attestation()
    {
        var quote = CreateQuotedQuote();
        var acceptedAtUtc = new DateTime(2026, 6, 21, 2, 0, 0, DateTimeKind.Utc);

        quote.Accept(
            acceptedByUserId: "customer-1",
            acceptedByName: "Jane Applicant",
            acceptedByTitle: "Chief Financial Officer",
            subjectivitiesAcknowledged: true,
            acceptedAtUtc);

        Assert.Equal(QuoteStatus.Accepted, quote.Status);
        Assert.Equal("customer-1", quote.AcceptedByUserId);
        Assert.Equal("Jane Applicant", quote.AcceptedByName);
        Assert.Equal("Chief Financial Officer", quote.AcceptedByTitle);
        Assert.True(quote.SubjectivitiesAcknowledged);
        Assert.Equal(acceptedAtUtc, quote.AcceptedAtUtc);
        Assert.Contains(
            quote.DomainEvents,
            domainEvent => domainEvent is QuoteAcceptedDomainEvent accepted
                && accepted.QuoteId == quote.Id
                && accepted.SubmissionId == quote.SubmissionId
                && accepted.OwnerUserId == quote.OwnerUserId
                && accepted.AcceptedByUserId == "customer-1"
                && accepted.OccurredAtUtc == acceptedAtUtc);
    }

    [Fact]
    public void Approved_adjusted_quote_can_be_accepted_and_keeps_adjusted_terms()
    {
        var quote = CreateReferredQuote();
        quote.AdjustReferral(
            "underwriter-1",
            adjustedPremium: 24_000m,
            adjustedRetention: 50_000m,
            updatedSubjectivities: "Evidence of MFA and EDR required before bind.",
            reason: "Adjusted for stronger reviewed controls.",
            notes: "Admin approval.",
            reviewedAtUtc: new DateTime(2026, 6, 21, 1, 0, 0, DateTimeKind.Utc));

        quote.Accept(
            acceptedByUserId: "customer-1",
            acceptedByName: "Jane Applicant",
            acceptedByTitle: "Chief Financial Officer",
            subjectivitiesAcknowledged: true,
            acceptedAtUtc: new DateTime(2026, 6, 21, 2, 0, 0, DateTimeKind.Utc));

        Assert.Equal(QuoteStatus.Accepted, quote.Status);
        Assert.Equal(24_000m, quote.Premium);
        Assert.Equal(50_000m, quote.Retention);
        Assert.Equal("Evidence of MFA and EDR required before bind.", quote.Subjectivities);
    }

    [Theory]
    [InlineData(QuoteStatus.Referred)]
    [InlineData(QuoteStatus.Declined)]
    public void Ineligible_quote_statuses_cannot_be_accepted(QuoteStatus status)
    {
        var quote = status == QuoteStatus.Referred
            ? CreateReferredQuote()
            : CreateDeclinedQuote();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            quote.Accept(
                acceptedByUserId: "customer-1",
                acceptedByName: "Jane Applicant",
                acceptedByTitle: "Chief Financial Officer",
                subjectivitiesAcknowledged: true,
                acceptedAtUtc: new DateTime(2026, 6, 21, 2, 0, 0, DateTimeKind.Utc)));

        Assert.Equal("Only quoted or approved quotes can be accepted.", exception.Message);
    }

    [Fact]
    public void Expired_quote_cannot_be_accepted()
    {
        var quote = CreateQuotedQuote();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            quote.Accept(
                acceptedByUserId: "customer-1",
                acceptedByName: "Jane Applicant",
                acceptedByTitle: "Chief Financial Officer",
                subjectivitiesAcknowledged: true,
                acceptedAtUtc: quote.ExpiresAtUtc.AddSeconds(1)));

        Assert.Equal("Expired quotes cannot be accepted.", exception.Message);
    }

    [Fact]
    public void Accepted_quote_cannot_be_accepted_again()
    {
        var quote = CreateQuotedQuote();
        quote.Accept(
            acceptedByUserId: "customer-1",
            acceptedByName: "Jane Applicant",
            acceptedByTitle: "Chief Financial Officer",
            subjectivitiesAcknowledged: true,
            acceptedAtUtc: new DateTime(2026, 6, 21, 2, 0, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            quote.Accept(
                acceptedByUserId: "customer-1",
                acceptedByName: "Jane Applicant",
                acceptedByTitle: "Chief Financial Officer",
                subjectivitiesAcknowledged: true,
                acceptedAtUtc: new DateTime(2026, 6, 21, 2, 5, 0, DateTimeKind.Utc)));

        Assert.Equal("Only quoted or approved quotes can be accepted.", exception.Message);
    }

    [Fact]
    public void Accepted_quote_can_bind_to_bound_policy_with_event_and_one_year_term()
    {
        var quote = CreateQuotedQuote();
        var acceptedAtUtc = new DateTime(2026, 6, 21, 2, 0, 0, DateTimeKind.Utc);
        var boundAtUtc = new DateTime(2026, 6, 21, 3, 0, 0, DateTimeKind.Utc);
        var effectiveDateUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        quote.Accept(
            "customer-1",
            "Jane Applicant",
            "Chief Financial Officer",
            subjectivitiesAcknowledged: true,
            acceptedAtUtc);

        var policy = Policy.BindFromAcceptedQuote(
            quote,
            policyNumber: "LIP-CYB-20260621-ABC12345",
            boundByUserId: "customer-1",
            effectiveDateUtc,
            boundAtUtc);
        quote.MarkBound(boundAtUtc);

        Assert.Equal(PolicyStatus.Bound, policy.Status);
        Assert.Equal(QuoteStatus.Bound, quote.Status);
        Assert.Equal(quote.Id, policy.QuoteId);
        Assert.Equal(quote.SubmissionId, policy.SubmissionId);
        Assert.Equal(quote.OwnerUserId, policy.OwnerUserId);
        Assert.Equal("LIP-CYB-20260621-ABC12345", policy.PolicyNumber);
        Assert.Equal(quote.Premium, policy.Premium);
        Assert.Equal(quote.RequestedLimit, policy.RequestedLimit);
        Assert.Equal(quote.Retention, policy.Retention);
        Assert.Equal(effectiveDateUtc, policy.EffectiveDateUtc);
        Assert.Equal(effectiveDateUtc.AddYears(1), policy.ExpirationDateUtc);
        Assert.Equal("customer-1", policy.BoundByUserId);
        Assert.Equal(boundAtUtc, policy.BoundAtUtc);
        Assert.Contains(
            policy.DomainEvents,
            domainEvent => domainEvent is PolicyBoundDomainEvent bound
                && bound.PolicyId == policy.Id
                && bound.PolicyNumber == policy.PolicyNumber);
    }

    private static Quote CreateQuotedQuote()
    {
        return Quote.Generate(
            Guid.Parse("9d5f6eb5-6450-43f7-ae2c-7a8f8a2f7d01"),
            "customer-1",
            premium: 12_000m,
            requestedLimit: 1_000_000m,
            retention: 10_000m,
            CyberRiskTier.Moderate,
            "BaselineCyber",
            ["MFA is implemented."],
            [],
            new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc));
    }

    private static Quote CreateReferredQuote()
    {
        return Quote.Generate(
            Guid.Parse("9d5f6eb5-6450-43f7-ae2c-7a8f8a2f7d02"),
            "customer-1",
            premium: 18_000m,
            requestedLimit: 5_000_000m,
            retention: 10_000m,
            CyberRiskTier.Severe,
            "HighRiskCyber",
            ["MFA evidence required."],
            ["Severe risk tier requires underwriter review."],
            new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc));
    }

    private static Quote CreateDeclinedQuote()
    {
        var quote = CreateReferredQuote();
        quote.DeclineReferral(
            "underwriter-1",
            "Risk is outside current appetite.",
            null,
            new DateTime(2026, 6, 21, 1, 0, 0, DateTimeKind.Utc));

        return quote;
    }
}
