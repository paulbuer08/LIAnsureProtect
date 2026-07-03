namespace LIAnsureProtect.Modules.Claims.Application.Commands.FileClaim;

/// <summary>Human-readable claim numbers, same style as policy numbers (<c>LIP-CYB-…</c>).</summary>
internal static class ClaimNumberGenerator
{
    public static string Create(DateTime filedAtUtc)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        return $"CLM-CYB-{filedAtUtc:yyyyMMdd}-{suffix}";
    }
}
