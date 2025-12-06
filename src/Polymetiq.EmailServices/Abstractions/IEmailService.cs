using MimeKit;

namespace Polymetiq.EmailServices;

public interface IEmailService
{
    /// <summary>
    /// Send the given message as email.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="configure">A handler for configuring how the body of the email is constructed.</param>
    /// <returns>EmailResult object.</returns>
    public Task<EmailResult> SendAsync(MailMessage message, Action<BodyBuilder>? configure = null);
}
