using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace LIAnsureProtect.Api.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed partial class PhilippineMobileNumberAttribute : ValidationAttribute
{
    public PhilippineMobileNumberAttribute()
        : base("Respondent mobile number must use a Philippine mobile format such as 09171234567 or +639171234567.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.ToString()))
            return true;

        var compact = PhoneSeparatorRegex().Replace(value.ToString()!.Trim(), string.Empty);
        return PhilippineDomesticMobileRegex().IsMatch(compact)
            || PhilippineInternationalMobileRegex().IsMatch(compact);
    }

    [GeneratedRegex("[\\s()\\-]")]
    private static partial Regex PhoneSeparatorRegex();

    [GeneratedRegex("^09[0-9]{9}$")]
    private static partial Regex PhilippineDomesticMobileRegex();

    [GeneratedRegex("^\\+?639[0-9]{9}$")]
    private static partial Regex PhilippineInternationalMobileRegex();
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed partial class PhilippineTelephoneNumberAttribute : ValidationAttribute
{
    public PhilippineTelephoneNumberAttribute()
        : base("Respondent telephone number must use a Philippine landline format such as 02 8123 4567 or +63 2 8123 4567.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.ToString()))
            return true;

        var compact = PhoneSeparatorRegex().Replace(value.ToString()!.Trim(), string.Empty);
        return PhilippineDomesticTelephoneRegex().IsMatch(compact)
            || PhilippineInternationalTelephoneRegex().IsMatch(compact);
    }

    [GeneratedRegex("[\\s()\\-]")]
    private static partial Regex PhoneSeparatorRegex();

    [GeneratedRegex("^0[2-8][0-9]{7,8}$")]
    private static partial Regex PhilippineDomesticTelephoneRegex();

    [GeneratedRegex("^\\+?63[2-8][0-9]{7,8}$")]
    private static partial Regex PhilippineInternationalTelephoneRegex();
}
