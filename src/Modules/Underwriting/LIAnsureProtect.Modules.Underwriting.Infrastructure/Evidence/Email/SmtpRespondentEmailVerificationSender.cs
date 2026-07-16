using System.Globalization;
using System.Net;
using System.Net.Mail;
using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Email;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Evidence.Email;

public sealed class SmtpRespondentEmailVerificationSender : IRespondentEmailVerificationSender
{
    public async Task<bool> SendAsync(
        RespondentEmailVerificationMessage message,
        CancellationToken cancellationToken)
    {
        var host = Environment.GetEnvironmentVariable("LIANSUREPROTECT_SMTP_HOST") ?? "localhost";
        var port = int.TryParse(
            Environment.GetEnvironmentVariable("LIANSUREPROTECT_SMTP_PORT"),
            CultureInfo.InvariantCulture,
            out var configuredPort) ? configuredPort : 1025;
        var from = Environment.GetEnvironmentVariable("LIANSUREPROTECT_SMTP_FROM")
            ?? "no-reply@liansureprotect.local";

        using var mail = new MailMessage(from, message.RecipientEmail)
        {
            Subject = "Verify your LIAnsureProtect respondent email",
            Body = $"Use this one-time verification code within 20 minutes:\n\n{message.VerificationCode}\n\n" +
                "This verifies access to the mailbox only. It does not verify the evidence response.",
            IsBodyHtml = false
        };
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = string.Equals(
                Environment.GetEnvironmentVariable("LIANSUREPROTECT_SMTP_TLS"),
                "true",
                StringComparison.OrdinalIgnoreCase)
        };
        var username = Environment.GetEnvironmentVariable("LIANSUREPROTECT_SMTP_USERNAME");
        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(
                username,
                Environment.GetEnvironmentVariable("LIANSUREPROTECT_SMTP_PASSWORD"));
        }

        try
        {
            await client.SendMailAsync(mail, cancellationToken);
            return true;
        }
        catch (SmtpException)
        {
            return false;
        }
    }
}
