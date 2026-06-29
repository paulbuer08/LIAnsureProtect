using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Platform.Abstractions.Security;
using LIAnsureProtect.Application.Quotes;
using LIAnsureProtect.Application.Quotes.Commands.CreateQuote;
using LIAnsureProtect.Application.Quotes.Rating;
using LIAnsureProtect.Application.Quotes.RatingProviders;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using Moq;

namespace LIAnsureProtect.UnitTests.Quotes;

public sealed class CreateQuoteCommandHandlerTests
{
    [Fact]
    public async Task Handle_Calls_Rating_Provider_And_Stores_Successful_Attempt()
    {
        var submission = CreateSubmittedSubmission();
        var command = CreateCommand(submission.Id);
        var quoteRepository = new Mock<IQuoteRepository>();
        Quote? savedQuote = null;
        QuoteRatingProviderAttempt? savedAttempt = null;
        quoteRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Quote>(), It.IsAny<CancellationToken>()))
            .Callback<Quote, CancellationToken>((quote, _) => savedQuote = quote)
            .Returns(Task.CompletedTask);
        quoteRepository
            .Setup(repository => repository.AddRatingProviderAttemptAsync(
                It.IsAny<QuoteRatingProviderAttempt>(),
                It.IsAny<CancellationToken>()))
            .Callback<QuoteRatingProviderAttempt, CancellationToken>((attempt, _) => savedAttempt = attempt)
            .Returns(Task.CompletedTask);
        var providerClient = new Mock<IRatingProviderClient>();
        providerClient
            .Setup(client => client.GetMarketIndicationAsync(
                It.IsAny<RatingProviderRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RatingProviderResult.Succeeded(
                providerName: "Contoso Specialty",
                marketDisposition: RatingProviderMarketDisposition.Quoted,
                providerReference: "CNT-REF-1001",
                providerQuoteNumber: "CNT-Q-9001",
                indicatedPremium: 12_500m,
                indicatedLimit: 1_000_000m,
                indicatedRetention: 10_000m,
                httpStatusCode: 200,
                attemptCount: 1,
                duration: TimeSpan.FromMilliseconds(120),
                completedAtUtc: new DateTime(2026, 6, 21, 1, 0, 0, DateTimeKind.Utc)));
        var handler = CreateHandler(
            submission,
            quoteRepository.Object,
            providerClient.Object);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(savedQuote);
        Assert.NotNull(savedAttempt);
        providerClient.Verify(
            client => client.GetMarketIndicationAsync(
                It.Is<RatingProviderRequest>(request =>
                    request.SubmissionId == submission.Id
                    && request.LocalPremium == savedQuote.Premium
                    && request.LocalStatus == savedQuote.Status),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(savedQuote.Id, savedAttempt.QuoteId);
        Assert.Equal(RatingProviderAttemptStatus.Succeeded, savedAttempt.Status);
        Assert.Equal("CNT-REF-1001", savedAttempt.ProviderReference);
        Assert.Equal("CNT-Q-9001", result.ProviderIndication.ProviderQuoteNumber);
        Assert.Equal("Quoted", result.ProviderIndication.MarketDisposition);
    }

    [Fact]
    public async Task Handle_Still_Creates_Local_Quote_When_Provider_Is_Unavailable()
    {
        var submission = CreateSubmittedSubmission();
        var quoteRepository = new Mock<IQuoteRepository>();
        Quote? savedQuote = null;
        QuoteRatingProviderAttempt? savedAttempt = null;
        quoteRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Quote>(), It.IsAny<CancellationToken>()))
            .Callback<Quote, CancellationToken>((quote, _) => savedQuote = quote)
            .Returns(Task.CompletedTask);
        quoteRepository
            .Setup(repository => repository.AddRatingProviderAttemptAsync(
                It.IsAny<QuoteRatingProviderAttempt>(),
                It.IsAny<CancellationToken>()))
            .Callback<QuoteRatingProviderAttempt, CancellationToken>((attempt, _) => savedAttempt = attempt)
            .Returns(Task.CompletedTask);
        var providerClient = new Mock<IRatingProviderClient>();
        providerClient
            .Setup(client => client.GetMarketIndicationAsync(
                It.IsAny<RatingProviderRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RatingProviderResult.Failed(
                providerName: "Contoso Specialty",
                status: RatingProviderAttemptStatus.Unavailable,
                marketDisposition: RatingProviderMarketDisposition.Unavailable,
                failureCategory: RatingProviderFailureCategory.Timeout,
                failureReason: "Provider did not respond before the configured timeout.",
                httpStatusCode: null,
                attemptCount: 3,
                duration: TimeSpan.FromSeconds(3),
                completedAtUtc: new DateTime(2026, 6, 21, 1, 2, 0, DateTimeKind.Utc)));
        var handler = CreateHandler(
            submission,
            quoteRepository.Object,
            providerClient.Object);

        var result = await handler.Handle(
            CreateCommand(submission.Id),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(savedQuote);
        Assert.NotNull(savedAttempt);
        Assert.Equal(QuoteStatus.Quoted, savedQuote.Status);
        Assert.Equal(RatingProviderAttemptStatus.Unavailable, savedAttempt.Status);
        Assert.Equal(RatingProviderFailureCategory.Timeout, savedAttempt.FailureCategory);
        Assert.Equal("Unavailable", result.ProviderIndication.Status);
        Assert.Equal("Provider did not respond before the configured timeout.", result.ProviderIndication.FailureReason);
    }

    private static CreateQuoteCommandHandler CreateHandler(
        Submission submission,
        IQuoteRepository quoteRepository,
        IRatingProviderClient providerClient)
    {
        var submissionRepository = new Mock<ISubmissionRepository>();
        submissionRepository
            .Setup(repository => repository.GetOwnedForUpdateAsync(
                submission.Id,
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        return new CreateQuoteCommandHandler(
            submissionRepository.Object,
            quoteRepository,
            unitOfWork.Object,
            new TestCurrentUser("auth0|owner-user-1"),
            new CyberRatingStrategySelector(
            [
                new HighRiskCyberRatingStrategy(),
                new BaselineCyberRatingStrategy()
            ]),
            providerClient);
    }

    private static Submission CreateSubmittedSubmission()
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "auth0|owner-user-1",
            new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc));
        submission.Submit();
        submission.ClearDomainEvents();

        return submission;
    }

    private static CreateQuoteCommand CreateCommand(Guid submissionId)
    {
        return new CreateQuoteCommand(
            submissionId,
            CyberIndustryClass.ProfessionalServices,
            AnnualRevenueBand.From10MTo50M,
            1_000_000m,
            10_000m,
            CyberSecurityControlStatus.Implemented,
            CyberSecurityControlStatus.Implemented,
            BackupMaturity.Mature,
            true,
            0,
            SensitiveDataExposure.Moderate);
    }

    private sealed class TestCurrentUser(string userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public string? UserId { get; } = userId;

        public string? Email => "owner@example.com";

        public IReadOnlyCollection<string> GetRoles()
        {
            return ["Customer"];
        }

        public bool IsInRole(string role)
        {
            return string.Equals(role, "Customer", StringComparison.Ordinal);
        }
    }
}
