namespace LIAnsureProtect.Modules.Notifications.Application;

/// <summary>
/// Maps a caller's roles to the team notification audiences they may read. Team membership comes from
/// the role claim, so no user directory is needed. The role names are a stable contract; the module
/// keeps its own copy rather than referencing the legacy Application security constants.
/// </summary>
public static class NotificationTeamAudiences
{
    private const string Underwriter = "Underwriter";
    private const string Admin = "Admin";

    private static readonly string[] InternalOpsAudiences =
    [
        NotificationAudiences.UnderwritingOperations,
        NotificationAudiences.BindingOperations
    ];

    /// <summary>
    /// Internal ops staff (Underwriter, Admin) see the underwriting- and binding-operations team
    /// inboxes. Everyone else sees no team audiences.
    /// </summary>
    public static IReadOnlyCollection<string> ForRoles(IEnumerable<string> roles)
    {
        foreach (var role in roles)
        {
            if (string.Equals(role, Underwriter, StringComparison.Ordinal)
                || string.Equals(role, Admin, StringComparison.Ordinal))
            {
                return InternalOpsAudiences;
            }
        }

        return [];
    }
}
