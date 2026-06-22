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
    public const string AdminAccess = "System.Admin";
}
