namespace LIAnsureProtect.Modules.Claims.Application.Documents;

/// <summary>
/// Single source of truth for claim-document upload governance (same numbers as the evidence
/// documents: 5 files per upload, 10 MB per file, 50 MB total, allow-listed content types).
/// </summary>
public static class ClaimDocumentUploadRules
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
