using LIAnsureProtect.Modules.Underwriting.Application;
using LIAnsureProtect.Modules.Underwriting.Domain;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class EfAiUnderwritingReviewRepository(UnderwritingDbContext dbContext)
    : IAiUnderwritingReviewRepository
{
    public async Task AddAsync(AiUnderwritingReview review, CancellationToken cancellationToken)
    {
        await dbContext.AiUnderwritingReviews.AddAsync(review, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
