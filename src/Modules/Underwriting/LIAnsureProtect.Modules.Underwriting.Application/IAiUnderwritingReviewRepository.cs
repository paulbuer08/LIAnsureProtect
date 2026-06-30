using LIAnsureProtect.Modules.Underwriting.Domain;

namespace LIAnsureProtect.Modules.Underwriting.Application;

/// <summary>
/// Persists AI underwriting reviews in the module's own context. <see cref="AddAsync"/> adds and saves
/// (a single-aggregate write), so no separate unit of work is needed for this slice.
/// </summary>
public interface IAiUnderwritingReviewRepository
{
    Task AddAsync(AiUnderwritingReview review, CancellationToken cancellationToken);
}
