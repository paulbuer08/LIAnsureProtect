using System.Collections.Concurrent;
using System.Net.Mail;
using DnsClient;
using DnsClient.Protocol;
using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Email;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Evidence.Email;

public sealed class DnsRespondentEmailDomainChecker : IRespondentEmailDomainChecker
{
    private static readonly TimeSpan PositiveCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan NegativeCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly Dictionary<string, string> CommonProviderSuggestions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["yaho.com"] = "yahoo.com", ["yaoo.com"] = "yahoo.com",
            ["yahho.com"] = "yahoo.com", ["gmal.com"] = "gmail.com",
            ["gmial.com"] = "gmail.com", ["outlok.com"] = "outlook.com",
            ["hotnail.com"] = "hotmail.com"
        };

    private readonly LookupClient lookupClient = new(new LookupClientOptions
    {
        Timeout = TimeSpan.FromSeconds(2), Retries = 1, UseCache = true,
        MinimumCacheTimeout = NegativeCacheDuration,
        MaximumCacheTimeout = PositiveCacheDuration
    });
    private readonly ConcurrentDictionary<string, CacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<EmailDomainCapabilityResult> CheckAsync(string emailAddress, CancellationToken cancellationToken)
    {
        if (!MailAddress.TryCreate(emailAddress?.Trim(), out var address))
            throw new ArgumentException("Enter a valid respondent email address.", nameof(emailAddress));

        var domain = address.Host.TrimEnd('.').ToLowerInvariant();
        if (cache.TryGetValue(domain, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow)
            return AddSuggestion(cached.Result, domain);

        EmailDomainCapabilityResult result;
        try
        {
            var mxResponse = await lookupClient.QueryAsync(domain, QueryType.MX, cancellationToken: cancellationToken);
            if (mxResponse.Header.ResponseCode == DnsHeaderResponseCode.NotExistentDomain)
                result = Undeliverable(domain, "This email domain does not exist.");
            else
            {
                var mxRecords = mxResponse.Answers.MxRecords().ToList();
                if (mxRecords.Any(record => string.IsNullOrEmpty(record.Exchange.Value.TrimEnd('.'))))
                    result = Undeliverable(domain, "This email domain declares that it cannot receive email.");
                else if (mxRecords.Count > 0)
                    result = new EmailDomainCapabilityResult(EmailDomainCapabilityStatus.MailCapable, domain, IsAuthoritative: true);
                else
                    result = await CheckAddressFallbackAsync(domain, cancellationToken);
            }
        }
        catch (DnsResponseException exception) when (exception.Code is DnsResponseCode.ServerFailure or DnsResponseCode.Refused)
        {
            result = Unverified(domain);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = Unverified(domain);
        }
        catch (Exception exception) when (exception is TimeoutException or IOException)
        {
            result = Unverified(domain);
        }

        if (cache.Count >= 1000) cache.Clear();
        cache[domain] = new CacheEntry(result, DateTime.UtcNow +
            (result.Status == EmailDomainCapabilityStatus.Undeliverable ? NegativeCacheDuration : PositiveCacheDuration));
        return AddSuggestion(result, domain);
    }

    private async Task<EmailDomainCapabilityResult> CheckAddressFallbackAsync(string domain, CancellationToken cancellationToken)
    {
        var aResponse = await lookupClient.QueryAsync(domain, QueryType.A, cancellationToken: cancellationToken);
        var aaaaResponse = await lookupClient.QueryAsync(domain, QueryType.AAAA, cancellationToken: cancellationToken);
        if (aResponse.Header.ResponseCode == DnsHeaderResponseCode.NotExistentDomain && aaaaResponse.Header.ResponseCode == DnsHeaderResponseCode.NotExistentDomain)
            return Undeliverable(domain, "This email domain does not exist.");

        return aResponse.Answers.ARecords().Any() || aaaaResponse.Answers.AaaaRecords().Any()
            ? new EmailDomainCapabilityResult(
                EmailDomainCapabilityStatus.AddressFallback,
                domain,
                UserMessage: "This domain has no explicit mail server record. The response can continue, but the contact remains unverified.")
            : Undeliverable(domain, "This email domain has no mail server or address fallback.");
    }

    private static EmailDomainCapabilityResult Undeliverable(string domain, string message) =>
        new(EmailDomainCapabilityStatus.Undeliverable, domain, UserMessage: message, IsAuthoritative: true);

    private static EmailDomainCapabilityResult Unverified(string domain) =>
        new(EmailDomainCapabilityStatus.Unverified, domain,
            UserMessage: "The email domain could not be checked right now. The evidence response can continue, but verification remains pending.");

    private static EmailDomainCapabilityResult AddSuggestion(EmailDomainCapabilityResult result, string domain) =>
        CommonProviderSuggestions.TryGetValue(domain, out var suggestion) ? result with { Suggestion = suggestion } : result;

    private sealed record CacheEntry(EmailDomainCapabilityResult Result, DateTime ExpiresAtUtc);
}
