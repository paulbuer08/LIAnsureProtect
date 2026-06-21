namespace LIAnsureProtect.Application.Policies.Commands.BindPolicy;

internal static class PolicyNumberGenerator
{
    public static string Create(DateTime boundAtUtc)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        return $"LIP-CYB-{boundAtUtc:yyyyMMdd}-{suffix}";
    }
}
