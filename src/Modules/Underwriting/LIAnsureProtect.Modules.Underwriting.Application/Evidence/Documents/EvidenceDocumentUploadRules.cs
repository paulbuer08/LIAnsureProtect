namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;

/// <summary>
/// The single source of truth for evidence-document upload limits and allowed content types.
/// Enforced by the upload workflow and exposed to clients through the evidence reference-data
/// query, so the UI and the server can never disagree about the rules.
/// </summary>
public static class EvidenceDocumentUploadRules
{
    public const int MaximumDocumentCount = 5;
    public const long MaximumDocumentSizeBytes = 10 * 1024 * 1024;
    public const long MaximumTotalDocumentSizeBytes = 50 * 1024 * 1024;

    public static IReadOnlyDictionary<string, string> AllowedExtensionsByContentType { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = ".pdf",
            ["image/png"] = ".png",
            ["image/jpeg"] = ".jpg",
            ["text/plain"] = ".txt",
            ["text/csv"] = ".csv",
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx"
        };
}
