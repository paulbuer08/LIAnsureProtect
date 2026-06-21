using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.UnitTests.Quotes;

public sealed class QuoteUnderwritingReviewTests
{
    [Fact]
    public void Approve_records_review_snapshot_and_domain_event_for_referred_quote()
    {
        var quote = CreateReferredQuote();
        var reviewedAtUtc = new DateTime(2026, 6, 21, 1, 0, 0, DateTimeKind.Utc);

        var review = quote.ApproveReferral(
            "underwriter-1",
            "Controls are acceptable after manual review.",
            "MFA rollout evidence reviewed.",
            reviewedAtUtc);

        Assert.Equal(QuoteStatus.Approved, quote.Status);
        Assert.Equal("underwriter-1", quote.ReviewedByUserId);
        Assert.Equal(reviewedAtUtc, quote.ReviewedAtUtc);
        Assert.Equal("Controls are acceptable after manual review.", quote.UnderwritingDecisionReason);
        Assert.Equal("MFA rollout evidence reviewed.", quote.UnderwritingDecisionNotes);
        Assert.Equal(QuoteUnderwritingDecision.Approved, review.Decision);
        Assert.Equal(quote.Premium, review.PremiumAfter);
        Assert.Equal(quote.Retention, review.RetentionAfter);
        Assert.Contains(
            quote.DomainEvents,
            domainEvent => domainEvent is QuoteUnderwritingDecisionRecordedDomainEvent recorded
                && recorded.QuoteId == quote.Id
                && recorded.Decision == QuoteUnderwritingDecision.Approved);
    }

    [Fact]
    public void Adjust_updates_terms_and_records_before_and_after_values()
    {
        var quote = CreateReferredQuote();
        var originalPremium = quote.Premium;
        var originalRetention = quote.Retention;

        var review = quote.AdjustReferral(
            "underwriter-1",
            adjustedPremium: 22_500m,
            adjustedRetention: 25_000m,
            updatedSubjectivities: "MFA evidence required before bind.",
            reason: "Premium and retention adjusted for severe risk controls.",
            notes: null,
            reviewedAtUtc: new DateTime(2026, 6, 21, 1, 5, 0, DateTimeKind.Utc));

        Assert.Equal(QuoteStatus.Approved, quote.Status);
        Assert.Equal(22_500m, quote.Premium);
        Assert.Equal(25_000m, quote.Retention);
        Assert.Equal("MFA evidence required before bind.", quote.Subjectivities);
        Assert.Equal(QuoteUnderwritingDecision.Adjusted, review.Decision);
        Assert.Equal(originalPremium, review.PremiumBefore);
        Assert.Equal(22_500m, review.PremiumAfter);
        Assert.Equal(originalRetention, review.RetentionBefore);
        Assert.Equal(25_000m, review.RetentionAfter);
    }

    [Fact]
    public void Decline_prevents_later_review()
    {
        var quote = CreateReferredQuote();
        quote.DeclineReferral(
            "underwriter-1",
            "Risk is outside current appetite.",
            null,
            new DateTime(2026, 6, 21, 1, 10, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            quote.ApproveReferral(
                "underwriter-1",
                "Trying to approve after decline.",
                null,
                new DateTime(2026, 6, 21, 1, 15, 0, DateTimeKind.Utc)));

        Assert.Equal("Only referred quotes can be reviewed.", exception.Message);
    }

    [Fact]
    public void Approve_requires_reason()
    {
        var quote = CreateReferredQuote();

        var exception = Assert.Throws<ArgumentException>(() =>
            quote.ApproveReferral(
                "underwriter-1",
                " ",
                null,
                new DateTime(2026, 6, 21, 1, 20, 0, DateTimeKind.Utc)));

        Assert.Equal("Review reason is required. (Parameter 'reason')", exception.Message);
    }

    private static Quote CreateReferredQuote()
    {
        return Quote.Generate(
            Guid.Parse("9d5f6eb5-6450-43f7-ae2c-7a8f8a2f7d01"),
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
}
