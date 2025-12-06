namespace Polymetiq.EmailServices;

public class MailMessageRecipients
{
    public List<string> To { get; } = [];

    public List<string> Cc { get; } = [];

    public List<string> Bcc { get; } = [];
}
