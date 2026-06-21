using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes;

public interface IQuoteRepository
{
    Task AddAsync(Quote quote, CancellationToken cancellationToken);
}
