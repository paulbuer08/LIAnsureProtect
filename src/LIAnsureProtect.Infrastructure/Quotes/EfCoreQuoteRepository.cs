using LIAnsureProtect.Application.Quotes;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Infrastructure.Persistence;

namespace LIAnsureProtect.Infrastructure.Quotes;

public sealed class EfCoreQuoteRepository(SubmissionDbContext dbContext) : IQuoteRepository
{
    public async Task AddAsync(Quote quote, CancellationToken cancellationToken)
    {
        await dbContext.Quotes.AddAsync(quote, cancellationToken);
    }
}
