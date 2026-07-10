namespace LIAnsureProtect.Application.Policies.Queries;

public static class PolicyCoverageState
{
    public static string Compute(
        string contractualStatus,
        DateTime effectiveDateUtc,
        DateTime expirationDateUtc,
        DateTime asOfUtc)
    {
        if (string.Equals(contractualStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            return "Cancelled";

        if (asOfUtc < effectiveDateUtc)
            return "Scheduled";

        return asOfUtc < expirationDateUtc ? "Active" : "Expired";
    }
}
