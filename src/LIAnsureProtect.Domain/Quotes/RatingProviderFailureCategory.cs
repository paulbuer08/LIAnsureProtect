namespace LIAnsureProtect.Domain.Quotes;

public enum RatingProviderFailureCategory
{
    None = 0,
    Timeout = 1,
    ServerError = 2,
    RateLimited = 3,
    ValidationRejected = 4,
    CircuitOpen = 5,
    Unexpected = 6
}
