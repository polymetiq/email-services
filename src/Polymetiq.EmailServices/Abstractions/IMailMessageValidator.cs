using Microsoft.Extensions.Localization;

namespace Polymetiq.EmailServices;

/// <summary>
/// A <see cref="MailMessage"/> validator that reports localized string error messages associated with specific properties
/// so that users filling out email details from a user interface can receive precise error feedback.
/// </summary>
public interface IMailMessageValidator
{
    /// <summary>
    /// Validates a <see cref="MailMessage"/> to ensure it will properly submit an email if sent.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task<Dictionary<string, List<LocalizedString>>> ValidateAsync(MailMessage message);
}
