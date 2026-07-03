using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Application.Queries.GetMyClaimDetail;
using LIAnsureProtect.Modules.Claims.Application.Queries.ListMyClaims;
using Moq;

namespace LIAnsureProtect.UnitTests.Modules.Claims;

public sealed class ClaimQueriesTests
{
    private readonly Mock<IClaimsReader> claimsReader = new();

    [Fact]
    public async Task ListMyClaims_Returns_Owner_Scoped_Claims()
    {
        var summary = new ClaimResult(
            Guid.NewGuid(),
            "CLM-CYB-20260401-AAAAAAAA",
            Guid.NewGuid(),
            "LIP-CYB-20260101-AAAAAAAA",
            "RansomwareExtortion",
            new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc),
            "Filed",
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        claimsReader
            .Setup(reader => reader.ListOwnerClaimsAsync("customer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([summary]);
        var handler = new ListMyClaimsQueryHandler(claimsReader.Object, new TestClaimsCurrentUser("customer-1"));

        var result = await handler.Handle(new ListMyClaimsQuery(), CancellationToken.None);

        var item = Assert.Single(result.Claims);
        Assert.Equal(summary, item);
    }

    [Fact]
    public async Task ListMyClaims_Requires_An_Authenticated_User()
    {
        var handler = new ListMyClaimsQueryHandler(claimsReader.Object, new TestClaimsCurrentUser(null));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new ListMyClaimsQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task GetMyClaimDetail_Scopes_The_Read_To_The_Caller()
    {
        var claimId = Guid.NewGuid();
        claimsReader
            .Setup(reader => reader.GetOwnerClaimDetailAsync("customer-1", claimId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimDetailResult?)null);
        var handler = new GetMyClaimDetailQueryHandler(claimsReader.Object, new TestClaimsCurrentUser("customer-1"));

        var result = await handler.Handle(new GetMyClaimDetailQuery(claimId), CancellationToken.None);

        Assert.Null(result);
        claimsReader.Verify(
            reader => reader.GetOwnerClaimDetailAsync("customer-1", claimId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
