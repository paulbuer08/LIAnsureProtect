namespace LIAnsureProtect.Infrastructure.Quotes.RatingProviders;

internal sealed class RatingProviderAttemptCountingHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Options.TryGetValue(RatingProviderAttemptCounter.OptionsKey, out var counter))
            counter.Increment();

        return base.SendAsync(request, cancellationToken);
    }
}
