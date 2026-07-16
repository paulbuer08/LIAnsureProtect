using System.Security.Cryptography;
using System.Text;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence.Email;

public sealed record RespondentEmailVerificationMessage(
    string RecipientEmail,
    string VerificationCode,
    DateTime ExpiresAtUtc);

public interface IRespondentEmailVerificationSender
{
    Task<bool> SendAsync(RespondentEmailVerificationMessage message, CancellationToken cancellationToken);
}

public sealed record RequestRespondentEmailVerificationCommand(Guid EvidenceRequestId, Guid ResponseId)
    : IRequest<RespondentEmailVerificationResult?>;

public sealed record VerifyRespondentEmailCommand(Guid EvidenceRequestId, Guid ResponseId, string VerificationCode)
    : IRequest<RespondentEmailVerificationResult?>;

public sealed record RespondentEmailVerificationResult(
    Guid ResponseId,
    string Status,
    DateTime? SentAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime? VerifiedAtUtc);

public sealed class RequestRespondentEmailVerificationCommandHandler(
    IEvidenceRequestRepository repository,
    IRespondentEmailVerificationSender sender,
    ICurrentUser currentUser)
    : IRequestHandler<RequestRespondentEmailVerificationCommand, RespondentEmailVerificationResult?>
{
    public async Task<RespondentEmailVerificationResult?> Handle(
        RequestRespondentEmailVerificationCommand request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = CurrentEvidenceUser.GetRequiredUserId(
            currentUser,
            "An authenticated owner user id is required to verify respondent email.");
        var response = await repository.GetResponseForOwnerAsync(
            request.EvidenceRequestId,
            request.ResponseId,
            ownerUserId,
            cancellationToken);
        if (response is null) return null;

        var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var nowUtc = DateTime.UtcNow;
        var expiresAtUtc = nowUtc.AddMinutes(20);
        response.BeginEmailVerification(Hash(code), nowUtc, expiresAtUtc);
        await repository.SaveChangesAsync(cancellationToken);

        if (!await sender.SendAsync(
                new RespondentEmailVerificationMessage(response.RespondentEmail, code, expiresAtUtc),
                cancellationToken))
        {
            response.RecordEmailVerificationDeliveryFailed();
            await repository.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("The verification email could not be sent. Try again later.");
        }

        return ToResult(response);
    }

    internal static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    internal static RespondentEmailVerificationResult ToResult(Domain.Evidence.QuoteEvidenceResponse response) =>
        new(response.Id, response.EmailVerificationStatus, response.EmailVerificationSentAtUtc,
            response.EmailVerificationExpiresAtUtc, response.EmailVerifiedAtUtc);
}

public sealed class VerifyRespondentEmailCommandHandler(
    IEvidenceRequestRepository repository,
    ICurrentUser currentUser)
    : IRequestHandler<VerifyRespondentEmailCommand, RespondentEmailVerificationResult?>
{
    public async Task<RespondentEmailVerificationResult?> Handle(
        VerifyRespondentEmailCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.VerificationCode) || request.VerificationCode.Trim().Length > 100)
            throw new ArgumentException("Enter the verification code from the email.", nameof(request));
        var ownerUserId = CurrentEvidenceUser.GetRequiredUserId(
            currentUser,
            "An authenticated owner user id is required to verify respondent email.");
        var response = await repository.GetResponseForOwnerAsync(
            request.EvidenceRequestId,
            request.ResponseId,
            ownerUserId,
            cancellationToken);
        if (response is null) return null;

        response.VerifyEmail(
            RequestRespondentEmailVerificationCommandHandler.Hash(request.VerificationCode.Trim()),
            DateTime.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
        return RequestRespondentEmailVerificationCommandHandler.ToResult(response);
    }
}
