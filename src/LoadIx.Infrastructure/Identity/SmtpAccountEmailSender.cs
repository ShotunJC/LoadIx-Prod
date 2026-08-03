using System.Net;
using System.Net.Mail;
using LoadIx.Application.Security;
using LoadIx.Infrastructure.Resilience;
using Microsoft.Extensions.Options;

namespace LoadIx.Infrastructure.Identity;

public sealed class AccountEmailOptions
{
    public const string SectionName = "AccountEmail";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool EnableSsl { get; set; }
}

public sealed class SmtpAccountEmailSender(
    IOptions<AccountEmailOptions> options,
    TransientOperationExecutor operations) : IAccountEmailSender
{
    private readonly AccountEmailOptions _options = options.Value;

    public Task SendInvitationAsync(
        string email,
        string displayName,
        Uri acceptanceUri,
        CancellationToken cancellationToken) =>
        SendAsync(
            email,
            "Your LoadIx invitation",
            $"Hello {displayName},\n\nAccept your LoadIx invitation:\n{acceptanceUri}",
            cancellationToken);

    public Task SendPasswordResetAsync(
        string email,
        Uri resetUri,
        CancellationToken cancellationToken) =>
        SendAsync(
            email,
            "Reset your LoadIx password",
            $"Reset your LoadIx password using this one-time link:\n{resetUri}",
            cancellationToken);

    public Task SendEmailConfirmationAsync(
        string email,
        Uri confirmationUri,
        CancellationToken cancellationToken) =>
        SendAsync(
            email,
            "Confirm your LoadIx email",
            $"Confirm your LoadIx email address:\n{confirmationUri}",
            cancellationToken);

    public async Task SendAsync(
        string email,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        await operations.ExecuteAsync(
            "smtp",
            async token =>
            {
                using var message = new MailMessage(_options.FromAddress, email, subject, body);
                using var client = new SmtpClient(_options.Host, _options.Port)
                {
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = string.IsNullOrWhiteSpace(_options.Username)
                        ? CredentialCache.DefaultNetworkCredentials
                        : new NetworkCredential(_options.Username, _options.Password),
                    EnableSsl = _options.EnableSsl,
                };

                await client.SendMailAsync(message, token);
            },
            cancellationToken);
    }
}
