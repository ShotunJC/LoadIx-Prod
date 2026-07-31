namespace LoadIx.Application.Security;

public interface IAccountEmailSender
{
    Task SendAsync(
        string email,
        string subject,
        string body,
        CancellationToken cancellationToken);

    Task SendInvitationAsync(
        string email,
        string displayName,
        Uri acceptanceUri,
        CancellationToken cancellationToken);

    Task SendPasswordResetAsync(
        string email,
        Uri resetUri,
        CancellationToken cancellationToken);

    Task SendEmailConfirmationAsync(
        string email,
        Uri confirmationUri,
        CancellationToken cancellationToken);
}
