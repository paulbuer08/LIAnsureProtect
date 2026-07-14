using LIAnsureProtect.Application.Quotes.Queries.ListQuoteReferrals;
using MediatR;
using Moq;

namespace LIAnsureProtect.UnitTests.Application.Quotes;

public sealed class SearchQuoteReferralsQueryHandlerTests
{
    [Fact]
    public async Task Handle_Filters_The_Complete_Cached_Queue()
    {
        var sender = new Mock<ISender>();
        var matching = CreateReferral("High", "Urgent", "underwriter-1", needsAttention: 1);
        var other = CreateReferral("Low", "Normal", null, needsAttention: 0);
        sender.Setup(candidate => candidate.Send(
                It.IsAny<ListQuoteReferralsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListQuoteReferralsResult([matching, other]));
        var handler = new SearchQuoteReferralsQueryHandler(sender.Object);

        var result = await handler.Handle(
            new SearchQuoteReferralsQuery(
                RiskTier: "High",
                Priority: "Urgent",
                Assignment: "assigned",
                EvidenceState: "attention"),
            CancellationToken.None);

        Assert.Equal(matching, Assert.Single(result.QuoteReferrals));
        sender.Verify(candidate => candidate.Send(
            It.IsAny<ListQuoteReferralsQuery>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static QuoteReferralResult CreateReferral(
        string riskTier,
        string priority,
        string? assignedUserId,
        int needsAttention)
    {
        return new QuoteReferralResult(
            Guid.NewGuid(), Guid.NewGuid(), "owner-1", 1_000m, 1_000_000m, 10_000m,
            riskTier, "Referred", [], [], DateTime.UtcNow, DateTime.UtcNow.AddDays(30),
            new QuoteReferralOperationsSummaryResult(
                assignedUserId, priority, DateTime.UtcNow.AddDays(1), false, "Open", 0, null),
            new QuoteReferralEvidenceSummaryResult(
                1, 0, 0, 0, needsAttention, 0, DateTime.UtcNow.AddDays(2), false, null));
    }
}
