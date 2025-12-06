using Microsoft.Extensions.Localization;

namespace Polymetiq.EmailServices.Services;

public sealed class MailMessageValidator : IMailMessageValidator
{
    private readonly IEmailAddressValidator _emailAddressValidator;

    private IStringLocalizer S { get; }

    public MailMessageValidator(IEmailAddressValidator emailAddressValidator,
        IStringLocalizer<MailMessageValidator> stringLocalizer)
    {
        _emailAddressValidator = emailAddressValidator;
        S = stringLocalizer;
    }

    public Task<Dictionary<string, List<LocalizedString>>> ValidateAsync(MailMessage message)
    {
        var errors = new Dictionary<string, List<LocalizedString>>();

        var invalidSender = message.GetSender()
            .Where(address => !_emailAddressValidator.Validate(address))
            .Select(address => S["Invalid email address for the sender: '{0}'.", address]);

        AddError(errors, nameof(message.From), invalidSender);

        var recipients = message.GetRecipients();

        var invalidTo = recipients.To
            .Where(address => !_emailAddressValidator.Validate(address))
            .Select(address => S["Invalid email address for the recipient: '{0}'.", address]);

        AddError(errors, nameof(message.To), invalidTo);

        var invalidCc = recipients.Cc
            .Where(address => !_emailAddressValidator.Validate(address))
            .Select(address => S["Invalid email address for the recipient: '{0}'.", address]);

        AddError(errors, nameof(message.Cc), invalidCc);

        var invalidBcc = recipients.Bcc
            .Where(address => !_emailAddressValidator.Validate(address))
            .Select(address => S["Invalid email address for the recipient: '{0}'.", address]);

        AddError(errors, nameof(message.Bcc), invalidBcc);

        var invalidReplayTo = message.GetReplyTo()
            .Where(address => !_emailAddressValidator.Validate(address))
            .Select(address => S["Invalid email address for the recipient: '{0}'.", address]);

        AddError(errors, nameof(message.ReplyTo), invalidReplayTo);

        if (recipients.To.Count == 0 && recipients.Cc.Count == 0 && recipients.Bcc.Count == 0)
        {
            AddError(errors, string.Empty, [S["The mail message should have at least one of these headers: To, Cc or Bcc."]]);
        }

        return Task.FromResult(errors);
    }

    private static void AddError(Dictionary<string, List<LocalizedString>> errors, string key, IEnumerable<LocalizedString> errorMessages)
    {
        if (!errorMessages.Any())
        {
            return;
        }

        errors.TryAdd(key, []);
        errors[key].AddRange(errorMessages);
    }
}
