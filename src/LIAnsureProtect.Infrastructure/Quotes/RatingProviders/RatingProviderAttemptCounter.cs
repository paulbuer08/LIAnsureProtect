using System.Net.Http;

namespace LIAnsureProtect.Infrastructure.Quotes.RatingProviders;

internal sealed class RatingProviderAttemptCounter
{
    public static readonly HttpRequestOptionsKey<RatingProviderAttemptCounter> OptionsKey =
        new("LIAnsureProtect.RatingProviderAttemptCounter");

    public int Count { get; private set; }

    public void Increment()
    {
        Count++;
    }
}
