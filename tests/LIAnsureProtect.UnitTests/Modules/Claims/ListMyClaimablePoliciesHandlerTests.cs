using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Application.Queries.ListMyClaimablePolicies;
using Moq;

namespace LIAnsureProtect.UnitTests.Modules.Claims;

public sealed class ListMyClaimablePoliciesHandlerTests
{
    private readonly Mock<IClaimsPolicyContextReader> policyContextReader = new();

    [Fact]
    public async Task Returns_The_Callers_Bound_Policies()
    {
        var snapshot = new ClaimsPolicySnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "LIP-CYB-20260101-AAAAAAAA",
            "customer-1",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            1_000_000m,
            25_000m,
            "Bound");
        policyContextReader
            .Setup(reader => reader.ListOwnedBoundPoliciesAsync("customer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([snapshot]);
        var handler = new ListMyClaimablePoliciesQueryHandler(
            policyContextReader.Object, new TestClaimsCurrentUser("customer-1"));

        var result = await handler.Handle(new ListMyClaimablePoliciesQuery(), CancellationToken.None);

        var policy = Assert.Single(result.Policies);
        Assert.Equal(snapshot.PolicyId, policy.PolicyId);
        Assert.Equal("LIP-CYB-20260101-AAAAAAAA", policy.PolicyNumber);
        Assert.Equal(1_000_000m, policy.Limit);
    }

    [Fact]
    public async Task Requires_An_Authenticated_User()
    {
        var handler = new ListMyClaimablePoliciesQueryHandler(
            policyContextReader.Object, new TestClaimsCurrentUser(null));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new ListMyClaimablePoliciesQuery(), CancellationToken.None));
    }
}
