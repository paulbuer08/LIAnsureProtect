namespace LIAnsureProtect.Modules.Notifications.Application;

/// <summary>
/// Maps a caller's roles to the team notification audiences they may read. Team membership comes from
/// the role claim, so no user directory is needed. The role names are a stable contract; the module
/// keeps its own copy rather than referencing the legacy Application security constants.
/// </summary>
public static class NotificationTeamAudiences
{
    private const string Underwriter = "Underwriter";
    private const string ClaimsAdjuster = "ClaimsAdjuster";
    private const string Admin = "Admin";

    /// <summary>
    /// Role-additive: Underwriters see the underwriting- and binding-operations inboxes,
    /// ClaimsAdjusters see the claims-operations inbox, Admin (superuser by design) sees all
    /// three, and everyone else sees no team audiences. Combined roles union their audiences.
    /// </summary>
    public static IReadOnlyCollection<string> ForRoles(IEnumerable<string> roles)
    {
        var audiences = new List<string>(3);

        foreach (var role in roles)
        {
            if (string.Equals(role, Underwriter, StringComparison.Ordinal)
                || string.Equals(role, Admin, StringComparison.Ordinal))
            {
                AddOnce(audiences, NotificationAudiences.UnderwritingOperations);
                AddOnce(audiences, NotificationAudiences.BindingOperations);
            }

            if (string.Equals(role, ClaimsAdjuster, StringComparison.Ordinal)
                || string.Equals(role, Admin, StringComparison.Ordinal))
            {
                AddOnce(audiences, NotificationAudiences.ClaimsOperations);
            }
        }

        return audiences;
    }

    private static void AddOnce(List<string> audiences, string audience)
    {
        if (!audiences.Contains(audience))
            audiences.Add(audience);
    }
}
