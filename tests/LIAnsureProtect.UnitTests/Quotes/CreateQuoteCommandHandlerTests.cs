using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Platform.Abstractions.Security;
using LIAnsureProtect.Application.Quotes;
using LIAnsureProtect.Application.Quotes.Assurance;
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
            .Setup(repository => repository.GetLatestOwnedForSubmissionAsync(
                submission.Id,
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Quote?)null);
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
        Assert.Equal(QuoteAssuranceStatus.EvidenceRequired, savedQuote.AssuranceStatus);
        Assert.Equal(4, savedQuote.EvidenceRequiredCount);
        Assert.Equal(5, savedQuote.ControlAssertions.Count);
        Assert.Equal("Jane Applicant", savedQuote.AttestedByName);
        Assert.Equal("CFO", savedQuote.AttestedByTitle);
    }

    [Fact]
    public async Task Handle_Still_Creates_Local_Quote_When_Provider_Is_Unavailable()
    {
        var submission = CreateSubmittedSubmission();
        var quoteRepository = new Mock<IQuoteRepository>();
        Quote? savedQuote = null;
        QuoteRatingProviderAttempt? savedAttempt = null;
        quoteRepository
            .Setup(repository => repository.GetLatestOwnedForSubmissionAsync(
                submission.Id,
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Quote?)null);
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

    [Fact]
    public async Task Handle_Returns_Existing_Quote_For_Submission_Without_Creating_Another()
    {
        var submission = CreateSubmittedSubmission();
        var existingQuote = Quote.Generate(
            submission.Id,
            "auth0|owner-user-1",
            6_500m,
            1_000_000m,
            10_000m,
            CyberRiskTier.Low,
            "BaselineCyber",
            ["Maintain MFA for privileged accounts."],
            [],
            new DateTime(2026, 6, 21, 1, 0, 0, DateTimeKind.Utc));
        existingQuote.ClearDomainEvents();
        var quoteRepository = new Mock<IQuoteRepository>();
        quoteRepository
            .Setup(repository => repository.GetLatestOwnedForSubmissionAsync(
                submission.Id,
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingQuote);
        var providerClient = new Mock<IRatingProviderClient>();
        var handler = CreateHandler(
            submission,
            quoteRepository.Object,
            providerClient.Object);

        var result = await handler.Handle(
            CreateCommand(submission.Id),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(existingQuote.Id, result.QuoteId);
        Assert.Equal("AlreadyCreated", result.ProviderIndication.Status);
        providerClient.Verify(
            client => client.GetMarketIndicationAsync(
                It.IsAny<RatingProviderRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        quoteRepository.Verify(
            repository => repository.AddAsync(
                It.IsAny<Quote>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        quoteRepository.Verify(
            repository => repository.AddRatingProviderAttemptAsync(
                It.IsAny<QuoteRatingProviderAttempt>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Reassessment_Creates_New_Version_And_Supersedes_Prior_Quote()
    {
        var submission = CreateSubmittedSubmission();
        var existingQuote = CreateQuoteWithMfaAssertion(submission.Id, "NotImplemented");
        Quote? savedQuote = null;
        var quoteRepository = new Mock<IQuoteRepository>();
        quoteRepository
            .Setup(repository => repository.GetLatestOwnedForSubmissionAsync(
                submission.Id,
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingQuote);
        quoteRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Quote>(), It.IsAny<CancellationToken>()))
            .Callback<Quote, CancellationToken>((quote, _) => savedQuote = quote)
            .Returns(Task.CompletedTask);
        quoteRepository
            .Setup(repository => repository.AddRatingProviderAttemptAsync(
                It.IsAny<QuoteRatingProviderAttempt>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var providerClient = CreateSuccessfulProviderClient();
        var handler = CreateHandler(submission, quoteRepository.Object, providerClient.Object);
        var command = CreateCommand(submission.Id) with { IsReassessment = true };

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(savedQuote);
        Assert.Equal(QuoteStatus.Superseded, existingQuote.Status);
        Assert.Equal(2, savedQuote.Version);
        Assert.Equal(existingQuote.Id, savedQuote.SupersedesQuoteId);
        Assert.Contains(savedQuote.ControlAssertions, assertion =>
            assertion.ControlType == ControlType.MultiFactorAuthentication
            && assertion.EvidenceRequired
            && assertion.EvidenceReason.Contains("improved control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_Reassessment_Rejects_Request_When_No_Control_Assertion_Changed()
    {
        var submission = CreateSubmittedSubmission();
        var existingQuote = CreateQuoteWithMfaAssertion(submission.Id, "Implemented");
        var quoteRepository = new Mock<IQuoteRepository>();
        quoteRepository
            .Setup(repository => repository.GetLatestOwnedForSubmissionAsync(
                submission.Id,
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingQuote);
        var providerClient = new Mock<IRatingProviderClient>();
        var handler = CreateHandler(submission, quoteRepository.Object, providerClient.Object);

        var exception = await Assert.ThrowsAsync<LIAnsureProtect.Application.Common.Exceptions.BusinessConflictException>(() => handler.Handle(
            CreateCommand(submission.Id) with { IsReassessment = true },
            TestContext.Current.CancellationToken));

        Assert.Equal("quote.reassessment.no_changes", exception.Code);
        Assert.Contains("at least one control answer", exception.PublicMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(QuoteStatus.Superseded, existingQuote.Status);
        providerClient.Verify(
            client => client.GetMarketIndicationAsync(
                It.IsAny<RatingProviderRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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

    private static Quote CreateQuoteWithMfaAssertion(Guid submissionId, string claimedState)
    {
        var createdAtUtc = new DateTime(2026, 6, 21, 1, 0, 0, DateTimeKind.Utc);
        var detailsJson = ControlAssurancePolicy.Evaluate(new CreateQuoteAssuranceInput(
                1_000_000m,
                Enum.Parse<CyberSecurityControlStatus>(claimedState),
                CyberSecurityControlStatus.Implemented,
                BackupMaturity.Mature,
                true,
                0,
                SensitiveDataExposure.Moderate))
            .Single(decision => decision.ControlType == ControlType.MultiFactorAuthentication)
            .DetailsJson;
        var quote = Quote.Generate(
            submissionId,
            "auth0|owner-user-1",
            6_500m,
            1_000_000m,
            10_000m,
            CyberRiskTier.Low,
            "BaselineCyber",
            [],
            [],
            createdAtUtc);
        quote.AddControlAssertion(ControlAssertion.Create(
            quote.Id,
            1,
            ControlType.MultiFactorAuthentication,
            claimedState,
            false,
            string.Empty,
            createdAtUtc,
            detailsJson));
        quote.ClearDomainEvents();
        return quote;
    }

    private static Mock<IRatingProviderClient> CreateSuccessfulProviderClient()
    {
        var providerClient = new Mock<IRatingProviderClient>();
        providerClient
            .Setup(client => client.GetMarketIndicationAsync(
                It.IsAny<RatingProviderRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RatingProviderRequest request, CancellationToken _) =>
                RatingProviderResult.Succeeded(
                    "Contoso Specialty",
                    RatingProviderMarketDisposition.Quoted,
                    "CNT-REASSESS",
                    "CNT-Q-REASSESS",
                    request.LocalPremium,
                    request.RequestedLimit,
                    request.Retention,
                    200,
                    1,
                    TimeSpan.FromMilliseconds(20),
                    new DateTime(2026, 6, 21, 2, 0, 0, DateTimeKind.Utc)));
        return providerClient;
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
            SensitiveDataExposure.Moderate,
            AttestationAccepted: true,
            AttestedByName: "Jane Applicant",
            AttestedByTitle: "CFO");
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
