using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Application.Commands.FileClaim;
using LIAnsureProtect.Modules.Claims.Domain;
using LIAnsureProtect.Platform.Abstractions.Security;
using Moq;

namespace LIAnsureProtect.UnitTests.Modules.Claims;

public sealed class FileClaimCommandHandlerTests
{
    private static readonly Guid PolicyId = Guid.NewGuid();
    private static readonly Guid SubmissionId = Guid.NewGuid();
    private static readonly DateTime EffectiveAtUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ExpirationAtUtc = new(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IClaimRepository> claimRepository = new();
    private readonly Mock<IClaimsPolicyContextReader> policyContextReader = new();

    private static ClaimsPolicySnapshot BoundPolicy(string ownerUserId = "customer-1", string status = "Bound")
        => new(
            PolicyId,
            SubmissionId,
            "LIP-CYB-20260101-AAAAAAAA",
            ownerUserId,
            EffectiveAtUtc,
            ExpirationAtUtc,
            1_000_000m,
            25_000m,
            status);

    private static FileClaimCommand ValidCommand()
        => new(
            PolicyId,
            ClaimIncidentType.BusinessEmailCompromise,
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
            "Finance mailbox compromised; fraudulent wire instructions sent.");

    private FileClaimCommandHandler CreateHandler(ICurrentUser? currentUser = null)
        => new(
            claimRepository.Object,
            policyContextReader.Object,
            currentUser ?? new TestClaimsCurrentUser("customer-1"));

    [Fact]
    public async Task Files_Claim_Against_Owned_Bound_Policy()
    {
        policyContextReader
            .Setup(reader => reader.GetForClaimFilingAsync(PolicyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BoundPolicy());
        Claim? persisted = null;
        claimRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Claim>(), It.IsAny<CancellationToken>()))
            .Callback<Claim, CancellationToken>((claim, _) => persisted = claim);

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(persisted);
        Assert.Equal(persisted!.Id, result!.ClaimId);
        Assert.StartsWith("CLM-CYB-", result.ClaimNumber, StringComparison.Ordinal);
        Assert.Equal("Filed", result.Status);
        Assert.Equal(PolicyId, persisted.PolicyId);
        Assert.Equal(SubmissionId, persisted.SubmissionId);
        Assert.Equal("customer-1", persisted.OwnerUserId);
        Assert.Equal("LIP-CYB-20260101-AAAAAAAA", persisted.PolicyNumberAtFiling);
        Assert.Equal(1_000_000m, persisted.PolicyLimitAtFiling);
        claimRepository.Verify(
            repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Returns_Null_When_Policy_Not_Found()
    {
        policyContextReader
            .Setup(reader => reader.GetForClaimFilingAsync(PolicyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimsPolicySnapshot?)null);

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.Null(result);
        claimRepository.Verify(
            repository => repository.AddAsync(It.IsAny<Claim>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Returns_Null_When_Caller_Does_Not_Own_The_Policy()
    {
        policyContextReader
            .Setup(reader => reader.GetForClaimFilingAsync(PolicyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BoundPolicy(ownerUserId: "someone-else"));

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.Null(result);
        claimRepository.Verify(
            repository => repository.AddAsync(It.IsAny<Claim>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Rejects_Policy_That_Is_Not_Bound()
    {
        policyContextReader
            .Setup(reader => reader.GetForClaimFilingAsync(PolicyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BoundPolicy(status: "Cancelled"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateHandler().Handle(ValidCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_Incident_Outside_The_Policy_Period()
    {
        policyContextReader
            .Setup(reader => reader.GetForClaimFilingAsync(PolicyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BoundPolicy());
        var command = ValidCommand() with
        {
            IncidentAtUtc = ExpirationAtUtc.AddDays(1),
            DiscoveredAtUtc = ExpirationAtUtc.AddDays(2)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Requires_An_Authenticated_User()
    {
        var handler = CreateHandler(new TestClaimsCurrentUser(userId: null));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(ValidCommand(), CancellationToken.None));
    }
}

internal sealed class TestClaimsCurrentUser(string? userId) : ICurrentUser
{
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);

    public string? UserId { get; } = userId;

    public string? Email => null;

    public IReadOnlyCollection<string> GetRoles() => [];

    public bool IsInRole(string role) => false;
}
