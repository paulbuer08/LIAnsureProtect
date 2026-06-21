namespace LIAnsureProtect.Application.Quotes.Rating;

public sealed class CyberRatingStrategySelector(IEnumerable<ICyberRatingStrategy> strategies)
    : ICyberRatingStrategySelector
{
    private readonly IReadOnlyList<ICyberRatingStrategy> strategies = strategies.ToList();

    public CyberRatingResult Rate(CyberRatingInput input)
    {
        var strategy = strategies.FirstOrDefault(strategy => strategy.CanRate(input))
            ?? throw new InvalidOperationException("No cyber rating strategy supports the requested risk profile.");

        return strategy.Rate(input);
    }
}
