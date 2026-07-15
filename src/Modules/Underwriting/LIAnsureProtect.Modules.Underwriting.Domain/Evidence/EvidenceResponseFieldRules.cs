using System.Net.Mail;
using System.Text.RegularExpressions;

namespace LIAnsureProtect.Modules.Underwriting.Domain.Evidence;

public static partial class EvidenceResponseFieldRules
{
    public const int RespondentNameMaxLength = 120;
    public const int RespondentTitleMaxLength = 120;
    public const int RespondentEmailMaxLength = 254;
    public const int MobileNumberMaxLength = 16;
    public const int TelephoneNumberMaxLength = 16;
    public const int ResponseTextMaxLength = 4000;
    public const int OtherConcernsMaxLength = 2000;
    public const int MaxPendingFollowUps = 5;

    public static string Required(string? value, string parameterName, string displayName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{displayName} is required.", parameterName);

        var trimmed = value.Trim();
        EnsureLength(trimmed, parameterName, displayName, maxLength);
        return trimmed;
    }

    public static string? Optional(string? value, string parameterName, string displayName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        EnsureLength(trimmed, parameterName, displayName, maxLength);
        return trimmed;
    }

    public static string Email(string value)
    {
        var trimmed = Required(
            value,
            nameof(value),
            "Respondent email",
            RespondentEmailMaxLength);
        if (!MailAddress.TryCreate(trimmed, out var address)
            || !string.Equals(address.Address, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Respondent email must be a valid email address.", nameof(value));
        }

        return trimmed;
    }

    public static string? PhilippineMobileNumber(string? value)
    {
        var compact = CompactNumber(value, nameof(value), "Respondent mobile number");
        if (compact is null)
            return null;

        var digits = compact.StartsWith('+') ? compact[1..] : compact;
        if (PhilippineDomesticMobileRegex().IsMatch(digits))
            return $"+63{digits[1..]}";

        if (PhilippineInternationalMobileRegex().IsMatch(digits))
            return $"+{digits}";

        throw new ArgumentException(
            "Respondent mobile number must use a Philippine mobile format such as 09171234567 or +639171234567.",
            nameof(value));
    }

    public static string? PhilippineTelephoneNumber(string? value)
    {
        var compact = CompactNumber(value, nameof(value), "Respondent telephone number");
        if (compact is null)
            return null;

        var digits = compact.StartsWith('+') ? compact[1..] : compact;
        if (PhilippineDomesticTelephoneRegex().IsMatch(digits))
            return $"+63{digits[1..]}";

        if (PhilippineInternationalTelephoneRegex().IsMatch(digits))
            return $"+{digits}";

        throw new ArgumentException(
            "Respondent telephone number must use a Philippine landline format such as 02 8123 4567 or +63 2 8123 4567.",
            nameof(value));
    }

    private static string? CompactNumber(string? value, string parameterName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var compact = PhoneSeparatorRegex().Replace(value.Trim(), string.Empty);
        if (!PhoneCharactersRegex().IsMatch(compact))
            throw new ArgumentException($"{displayName} contains unsupported characters.", parameterName);

        return compact;
    }

    private static void EnsureLength(string value, string parameterName, string displayName, int maxLength)
    {
        if (value.Length > maxLength)
            throw new ArgumentException($"{displayName} cannot exceed {maxLength} characters.", parameterName);
    }

    [GeneratedRegex("[\\s()\\-]")]
    private static partial Regex PhoneSeparatorRegex();

    [GeneratedRegex("^\\+?[0-9]+$")]
    private static partial Regex PhoneCharactersRegex();

    [GeneratedRegex("^09[0-9]{9}$")]
    private static partial Regex PhilippineDomesticMobileRegex();

    [GeneratedRegex("^639[0-9]{9}$")]
    private static partial Regex PhilippineInternationalMobileRegex();

    [GeneratedRegex("^0[2-8][0-9]{7,8}$")]
    private static partial Regex PhilippineDomesticTelephoneRegex();

    [GeneratedRegex("^63[2-8][0-9]{7,8}$")]
    private static partial Regex PhilippineInternationalTelephoneRegex();
}
