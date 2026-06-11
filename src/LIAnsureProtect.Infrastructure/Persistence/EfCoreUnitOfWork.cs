using LIAnsureProtect.Application.Common.Persistence;

namespace LIAnsureProtect.Infrastructure.Persistence;

public sealed class EfCoreUnitOfWork(SubmissionDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
