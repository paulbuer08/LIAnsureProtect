using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Application.Commands.ManageClaimFinancials;
using LIAnsureProtect.Modules.Claims.Domain;
using Moq;

namespace LIAnsureProtect.UnitTests.Modules.Claims;

public sealed class ClaimFinancialsHandlerTests
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
    public async Task SetClaimedAmount_Updates_For_The_Owner()
    {
        var claim = FileClaim();
        SetUpClaim(claim);
        var handler = new SetClaimedAmountCommandHandler(claimRepository.Object, new TestClaimsCurrentUser("customer-1"));

        var result = await handler.Handle(new SetClaimedAmountCommand(claim.Id, 250_000m), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(250_000m, result!.ClaimedAmount);
        Assert.Equal(250_000m, claim.ClaimedAmount);
        claimRepository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetClaimedAmount_Is_Owner_Scoped()
    {
        var claim = FileClaim(ownerUserId: "customer-2");
        SetUpClaim(claim);
        var handler = new SetClaimedAmountCommandHandler(claimRepository.Object, new TestClaimsCurrentUser("customer-1"));

        var result = await handler.Handle(new SetClaimedAmountCommand(claim.Id, 250_000m), CancellationToken.None);

        Assert.Null(result);
        claimRepository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetReserve_Records_History_For_The_Assigned_Adjuster()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));
        SetUpClaim(claim);
        var handler = new SetClaimReserveCommandHandler(claimRepository.Object, new TestClaimsCurrentUser("adjuster-1"));

        var result = await handler.Handle(
            new SetClaimReserveCommand(claim.Id, 150_000m, "Initial estimate."), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(150_000m, result!.ReserveAmount);
        Assert.Single(claim.ReserveChanges);
        claimRepository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetReserve_Returns_Null_For_Unknown_Claim()
    {
        SetUpClaim(null);
        var handler = new SetClaimReserveCommandHandler(claimRepository.Object, new TestClaimsCurrentUser("adjuster-1"));

        var result = await handler.Handle(
            new SetClaimReserveCommand(Guid.NewGuid(), 150_000m, "Initial estimate."), CancellationToken.None);

        Assert.Null(result);
    }
}
