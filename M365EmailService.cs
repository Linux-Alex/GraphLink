using Microsoft.Graph;
using Azure.Identity;
using Microsoft.Graph.Models;

public class M365EmailService
{
    private readonly GraphServiceClient _graphClient;
    private readonly AzureADConfig _config;

    public M365EmailService(AzureADConfig config)
    {
        _config = config;

        var credential = new ClientSecretCredential(
            config.TenantId,
            config.ClientId,
            config.ClientSecret);

        _graphClient = new GraphServiceClient(credential);
    }

    public bool IsEmailAllowed(string email)
    {
        return _config.AllowedAccounts.Any(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    public AllowedAccount? GetAllowedAccount(string email)
    {
        return _config.AllowedAccounts.FirstOrDefault(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsReceiverAllowed(string senderEmail, string receiverEmail)
    {
        var account = GetAllowedAccount(senderEmail);
        if (account == null) return false;

        foreach (var pattern in account.AllowedRecivers)
        {
            // Support wildcards like "*@domain.com"
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            if (System.Text.RegularExpressions.Regex.IsMatch(receiverEmail, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;
        }
        return false;
    }


    /// <summary>
    /// Read emails from a specified folder, optionally filtered by fromDate and limited by top count.
    /// </summary>
    public async Task<IEnumerable<Message>> ReadEmailsAsync(string userEmail, int top = 10, string folder = "Inbox", DateTimeOffset? fromDate = null)
    {
        var messages = await _graphClient.Users[userEmail]
            .MailFolders[folder]
            .Messages
            .GetAsync(req =>
            {
                req.QueryParameters.Top = top;
                req.QueryParameters.Orderby = new[] { "receivedDateTime desc" };
                req.QueryParameters.Expand = new[] { "attachments" };

                if (fromDate.HasValue)
                {
                    // Filter for messages received after fromDate
                    req.QueryParameters.Filter = $"receivedDateTime ge {fromDate.Value.UtcDateTime.ToString("o")}";
                }
            });

        return messages?.Value ?? Enumerable.Empty<Message>();
    }

    /// <summary>
    /// Send email with multiple recipients and optional attachments.
    /// </summary>
    public async Task SendEmailAsync(
     string fromUserEmail,
     IEnumerable<string> toRecipients,
     string subject,
     string body,
     IEnumerable<string>? ccRecipients = null,
     IEnumerable<string>? bccRecipients = null,
     List<EmailAttachment>? attachments = null)
    {
        var message = new Message
        {
            Subject = subject,
            Body = new ItemBody
            {
                ContentType = BodyType.Html,
                Content = body
            },
            ToRecipients = toRecipients
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => new Recipient
                {
                    EmailAddress = new EmailAddress { Address = email }
                })
                .ToList(),

            CcRecipients = ccRecipients?
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => new Recipient
                {
                    EmailAddress = new EmailAddress { Address = email }
                })
                .ToList(),

            BccRecipients = bccRecipients?
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => new Recipient
                {
                    EmailAddress = new EmailAddress { Address = email }
                })
                .ToList()
        };

        if (attachments != null && attachments.Any())
        {
            message.Attachments = attachments.Select(att => new FileAttachment
            {
                Name = att.Name,
                ContentType = att.ContentType,
                ContentBytes = Convert.FromBase64String(att.Base64Content),
                IsInline = false
            }).Cast<Attachment>().ToList();
        }

        var sendMailRequest = new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
        {
            Message = message,
            SaveToSentItems = true
        };

        await _graphClient.Users[fromUserEmail]
            .SendMail
            .PostAsync(sendMailRequest);
    }


}
