namespace Polymetiq.EmailServices;

public static class MailMessageExtensions
{
    private static char[] EmailsSeparator { get; } = [',', ';'];

    extension(MailMessage message)
    {
        public MailMessageRecipients GetRecipients()
        {
            var recipients = new MailMessageRecipients();
            recipients.To.AddRange(GetRecipients(message.To));
            recipients.Cc.AddRange(GetRecipients(message.Cc));
            recipients.Bcc.AddRange(GetRecipients(message.Bcc));

            return recipients;
        }

        public IEnumerable<string> GetSender()
            => string.IsNullOrWhiteSpace(message.From)
            ? []
            : GetRecipients(message.From);

        public IEnumerable<string> GetReplyTo()
            => string.IsNullOrWhiteSpace(message.ReplyTo)
            ? message.GetSender()
            : GetRecipients(message.ReplyTo);
    }

    private static string[] GetRecipients(string? recipients)
        => recipients?.Split(EmailsSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        ?? [];
}
