using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using MailKit.Net.Proxy;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Polymetiq.EmailServices.Services;

public sealed class SmtpEmailService : IEmailService
{
    private const string EmailExtension = ".eml";

    private readonly SmtpOptions _options;
    private readonly IEmailAddressValidator _emailAddressValidator;
    private readonly IMailMessageValidator _mailMessageValidator;
    private readonly ILogger _logger;

    private IStringLocalizer S { get;}

    public SmtpEmailService(
        IOptionsMonitor<SmtpOptions> options,
        IEmailAddressValidator emailAddressValidator,
        IMailMessageValidator mailMessageValidator,
        ILogger<SmtpEmailService> logger,
        IStringLocalizer<SmtpEmailService> stringLocalizer)
    {
        _options = options.CurrentValue;
        _emailAddressValidator = emailAddressValidator;
        _mailMessageValidator = mailMessageValidator;
        _logger = logger;
        S = stringLocalizer;
    }

    public async Task<EmailResult> SendAsync(MailMessage message, Action<BodyBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_options.IsEnabled)
        {
            return EmailResult.FailedResult(S["The SMTP Email Provider is disabled."]);
        }

        var validationErrors = await _mailMessageValidator.ValidateAsync(message);

        if (validationErrors.Count > 0)
        {
             return EmailResult.FailedResult(validationErrors);
        }

        var senderAddress = string.IsNullOrWhiteSpace(message.From)
            ? _options.DefaultSender
            : message.From;

        _logger.LogDebug("Attempting to send email to {Email}.", message.To);

        // Set the MailMessage.From, to avoid the confusion between DefaultSender (Author) and submitter (Sender).
        if (!string.IsNullOrWhiteSpace(senderAddress))
        {
            if (!_emailAddressValidator.Validate(senderAddress))
            {
                return EmailResult.FailedResult(nameof(message.From), S["Invalid email address for the sender: '{0}'.", senderAddress]);
            }

            message.From = senderAddress;
        }

        var mimeMessage = GetMimeMessage(message, configure);

        try
        {
            if (_options.DeliveryMethod == SmtpDeliveryMethod.Network)
            {
                var response = await SendOnlineMessageAsync(mimeMessage);

                return EmailResult.GetSuccessResult(response);
            }

            if (_options.DeliveryMethod == SmtpDeliveryMethod.SpecifiedPickupDirectory)
            {
                await SendOfflineMessageAsync(mimeMessage, _options.PickupDirectoryLocation!);

                return EmailResult.SuccessResult;
            }

            throw new NotSupportedException($"The '{_options.DeliveryMethod}' delivery method is not supported.");
        }
        catch (Exception ex)
        {
            return EmailResult.FailedResult([S["An error occurred while sending an email: '{0}'", ex.Message]]);
        }
    }

    private MimeMessage GetMimeMessage(MailMessage message, Action<BodyBuilder>? configure = null)
    {
        var mimeMessage = new MimeMessage();
        var submitterAddress = string.IsNullOrWhiteSpace(message.Sender)
            ? _options.DefaultSender
            : message.Sender;

        if (!string.IsNullOrEmpty(submitterAddress))
        {
            mimeMessage.Sender = MailboxAddress.Parse(submitterAddress);
        }

        mimeMessage.From.AddRange(message.GetSender().Select(MailboxAddress.Parse));

        var recipients = message.GetRecipients();
        mimeMessage.To.AddRange(recipients.To.Select(MailboxAddress.Parse));
        mimeMessage.Cc.AddRange(recipients.Cc.Select(MailboxAddress.Parse));
        mimeMessage.Bcc.AddRange(recipients.Bcc.Select(MailboxAddress.Parse));

        mimeMessage.ReplyTo.AddRange(message.GetReplyTo().Select(MailboxAddress.Parse));

        mimeMessage.Subject = message.Subject;

        var body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
        };

        foreach (var attachment in message.Attachments)
        {
            // Stream must not be null, otherwise it would try to get the filesystem path
            if (attachment.Stream != null)
            {
                body.Attachments.Add(attachment.Filename, attachment.Stream);
            }
        }

        configure?.Invoke(body);

        mimeMessage.Body = body.ToMessageBody();

        if (mimeMessage.Body is MultipartRelated related && related.OfType<TextPart>().FirstOrDefault(p => p.IsHtml) is { } htmlPart)
        {
            htmlPart.ContentTransferEncoding = ContentEncoding.Base64;
        }

        return mimeMessage;
    }

    /// <summary>
    /// Modifications had to be made to the built-in ToMessageBody since
    /// it doesn't create emails that conform to the rigid structure
    /// expected from some email clients like Gmail.
    /// 
    /// If you have related linked resources in your email, like the brand logo,
    /// then the structure needs to be:
    /// - multipart/related
    ///   - multipart/alternative
    ///     - text/plain
    ///     - text/html (references cid:logo)
    ///   - image/png
    ///   
    /// The default ToMessageBody inverts the placement of related & alternative and
    /// thus stops email clients from bothering to load the logo at all.
    /// </summary>
    /// <param name="bb"></param>
    /// <returns></returns>
    private MimeEntity ToMessageBody(BodyBuilder bb)
    {
        MultipartAlternative? alternative = null;
        MimeEntity? body = null;

        if (bb.TextBody != null) {
            var text = new TextPart ("plain") {
                Text = bb.TextBody
            };

            if (bb.HtmlBody != null) {
                alternative = new MultipartAlternative {
                    text
                };
                body = alternative;
            } else {
                body = text;
            }
        }

        if (bb.HtmlBody != null) {
            var text = new TextPart ("html") {
                Text = bb.HtmlBody
            };
            MimeEntity html;

            if (bb.LinkedResources.Count > 0) {
                var related = new MultipartRelated {
                    Root = text
                };

                foreach (var resource in bb.LinkedResources)
                    related.Add (resource);

                html = related;
            } else {
                html = text;
            }

            if (alternative != null)
                alternative.Add (html);
            else
                body = html;
        }

        if (bb.Attachments.Count > 0) {
            if (body is null && bb.Attachments.Count == 1)
                return bb.Attachments[0];

            var mixed = new Multipart ("mixed");

            if (body != null)
                mixed.Add (body);

            foreach (var attachment in bb.Attachments)
                mixed.Add (attachment);

            body = mixed;
        }

        return body ?? new TextPart ("plain") { Text = string.Empty };
    }

    private async Task<string> SendOnlineMessageAsync(MimeMessage message)
    {
        var secureSocketOptions = SecureSocketOptions.Auto;

        if (!_options.AutoSelectEncryption)
        {
            secureSocketOptions = _options.EncryptionMethod switch
            {
                SmtpEncryptionMethod.None => SecureSocketOptions.None,
                SmtpEncryptionMethod.SslTls => SecureSocketOptions.SslOnConnect,
                SmtpEncryptionMethod.StartTls => SecureSocketOptions.StartTls,
                _ => SecureSocketOptions.Auto,
            };
        }

        using var client = new SmtpClient();

        client.ServerCertificateValidationCallback = CertificateValidationCallback;

        await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions);

        if (_options.RequireCredentials)
        {
            if (_options.UseDefaultCredentials)
            {
                // There's no notion of 'UseDefaultCredentials' in MailKit, so empty credentials is passed in.
                await client.AuthenticateAsync(string.Empty, string.Empty);
            }
            else if (!string.IsNullOrWhiteSpace(_options.UserName))
            {
                await client.AuthenticateAsync(_options.UserName, _options.Password);
            }
        }

        if (!string.IsNullOrEmpty(_options.ProxyHost))
        {
            client.ProxyClient = new Socks5Client(_options.ProxyHost, _options.ProxyPort);
        }

        var response = await client.SendAsync(message);

        await client.DisconnectAsync(true);

        return response;
    }

    private static Task SendOfflineMessageAsync(MimeMessage message, string pickupDirectory)
    {
        var mailPath = Path.Combine(pickupDirectory, Guid.NewGuid().ToString() + EmailExtension);

        return message.WriteToAsync(mailPath, CancellationToken.None);
    }

    private bool CertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        const string logErrorMessage = "SMTP Server's certificate {CertificateSubject} issued by {CertificateIssuer} " +
                               "with thumbprint {CertificateThumbprint} and expiration date {CertificateExpirationDate} " +
                               "is considered invalid with {SslPolicyErrors} policy errors";

        _logger.LogError(logErrorMessage,
            certificate!.Subject,
            certificate.Issuer,
            certificate.GetCertHashString(),
            certificate.GetExpirationDateString(),
            sslPolicyErrors);

        if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors) && chain?.ChainStatus != null)
        {
            foreach (var chainStatus in chain.ChainStatus)
            {
                _logger.LogError("Status: {Status} - {StatusInformation}", chainStatus.Status, chainStatus.StatusInformation);
            }
        }

        return _options.IgnoreInvalidSslCertificate;
    }
}
