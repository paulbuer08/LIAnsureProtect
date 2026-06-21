namespace LIAnsureProtect.Application.Quotes.RatingProviders;

public interface IRatingProviderClient
{
    Task<RatingProviderResult> GetMarketIndicationAsync(
        RatingProviderRequest request,
        CancellationToken cancellationToken);
}
