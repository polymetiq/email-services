namespace Polymetiq.EmailServices;

/// <summary>
/// Represents a class that contains information for a mail message attachment.
/// </summary>
public class MailMessageAttachment
{
    public MailMessageAttachment(string filename, Stream stream)
    {
        Filename = filename;
        Stream = stream;
    }

    /// <summary>
    /// Gets or sets the attachment filename.
    /// </summary>
    public string Filename { get; set; } = "";

    /// <summary>
    /// Gets or sets the attachment file stream.
    /// </summary>
    public Stream Stream { get; set; }
}
