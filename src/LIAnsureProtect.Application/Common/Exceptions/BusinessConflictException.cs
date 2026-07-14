namespace LIAnsureProtect.Application.Common.Exceptions;

public sealed class BusinessConflictException(
    string code,
    string publicMessage) : Exception(publicMessage)
{
    public string Code { get; } = string.IsNullOrWhiteSpace(code)
        ? throw new ArgumentException("A public business-conflict code is required.", nameof(code))
        : code;

    public string PublicMessage { get; } = string.IsNullOrWhiteSpace(publicMessage)
        ? throw new ArgumentException("A public business-conflict message is required.", nameof(publicMessage))
        : publicMessage;
}
