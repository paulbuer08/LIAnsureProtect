namespace LIAnsureProtect.Application.Common.Security;

public static class ApplicationPolicies
{
    public const string CreateSubmission = "Submissions.Create";
    public const string ReadSubmission = "Submissions.Read";
    public const string SubmitSubmission = "Submissions.Submit";
    public const string CreateQuote = "Quotes.Create";
    public const string AcceptQuote = "Quotes.Accept";
    public const string UnderwriteQuote = "Quotes.Underwrite";
    public const string RespondToEvidenceRequest = "EvidenceRequests.Respond";
    public const string BindPolicy = "Policies.Bind";
    public const string ReadPolicy = "Policies.Read";
    public const string ReadNotifications = "Notifications.Read";
    public const string FileClaim = "Claims.File";
    public const string ReadClaim = "Claims.Read";
    public const string RespondToClaim = "Claims.Respond";
    public const string AdjudicateClaim = "Claims.Adjudicate";
    public const string AdminAccess = "System.Admin";
}
