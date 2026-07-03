using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Application.Commands.ManageClaimAdjudication;
using LIAnsureProtect.Modules.Claims.Application.Commands.RespondToClaimInformationRequest;
using LIAnsureProtect.Modules.Claims.Domain;
using Moq;

namespace LIAnsureProtect.UnitTests.Modules.Claims;

public sealed class ClaimAdjudicationHandlerTests
{
    private static readonly DateTime FiledAtUtc = new(2026, 3, 13, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IClaimRepository> claimRepository = new();

    private static Claim FileClaim(string ownerUserId = "customer-1")
    {
        var claim = Claim.File(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ownerUserId,
            "CLM-2026-0A1B2C3D",
            ClaimIncidentType.RansomwareExtortion,
            new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 12, 9, 30, 0, DateTimeKind.Utc),
            "Ransomware encrypted the file server.",
            "POL-2026-11111111",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            1_000_000m,
            25_000m,
            FiledAtUtc);
        claim.ClearDomainEvents();

        return claim;
    }

    private void SetUpClaim(Claim? claim)
    {
        claimRepository
            .Setup(repository => repository.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);
    }

    [Fact]
    public async Task AssignClaimToMe_Assigns_And_Saves()
    {
        var claim = FileClaim();
        SetUpClaim(claim);
        var handler = new AssignClaimToMeCommandHandler(claimRepository.Object, new TestClaimsCurrentUser("adjuster-1"));

        var result = await handler.Handle(new AssignClaimToMeCommand(claim.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("adjuster-1", result!.AssignedAdjusterUserId);
        Assert.Equal("UnderReview", result.Status);
        claimRepository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignClaimToMe_Returns_Null_For_Unknown_Claim()
    {
        SetUpClaim(null);
        var handler = new AssignClaimToMeCommandHandler(claimRepository.Object, new TestClaimsCurrentUser("adjuster-1"));

        var result = await handler.Handle(new AssignClaimToMeCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
        claimRepository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestClaimInformation_Creates_The_Request()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));
        SetUpClaim(claim);
        var handler = new RequestClaimInformationCommandHandler(claimRepository.Object, new TestClaimsCurrentUser("adjuster-1"));

        var result = await handler.Handle(
            new RequestClaimInformationCommand(claim.Id, "Proof of loss", "Please provide the forensic report."),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Proof of loss", result!.Title);
        Assert.False(result.IsAnswered);
        claimRepository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RespondToInformationRequest_Enforces_Ownership()
    {
        var claim = FileClaim(ownerUserId: "customer-2");
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));
        var request = claim.RequestInformation("adjuster-1", "Proof of loss", "Please provide it.", FiledAtUtc.AddHours(2));
        SetUpClaim(claim);
        var handler = new RespondToClaimInformationRequestCommandHandler(
            claimRepository.Object,
            new TestClaimsCurrentUser("customer-1"));

        var result = await handler.Handle(
            new RespondToClaimInformationRequestCommand(claim.Id, request.Id, "Attached."),
            CancellationToken.None);

        Assert.Null(result);
        claimRepository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RespondToInformationRequest_Answers_For_The_Owner()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));
        var request = claim.RequestInformation("adjuster-1", "Proof of loss", "Please provide it.", FiledAtUtc.AddHours(2));
        SetUpClaim(claim);
        var handler = new RespondToClaimInformationRequestCommandHandler(
            claimRepository.Object,
            new TestClaimsCurrentUser("customer-1"));

        var result = await handler.Handle(
            new RespondToClaimInformationRequestCommand(claim.Id, request.Id, "Forensic report attached."),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsAnswered);
        Assert.Equal("Forensic report attached.", result.ResponseText);
        claimRepository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
