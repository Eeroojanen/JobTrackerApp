using System;
using System.Text.RegularExpressions;

namespace JobTracker.Services;

public class GmailMessage
{
    public string Id { get; set; } = "";
    public string ThreadId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string From { get; set; } = "";
    public string Snippet { get; set; } = "";
    public string BodyText { get; set; } = "";
    public DateTime ReceivedAt { get; set; }

    public string SenderEmail
    {
        get
        {
            var match = Regex.Match(From, @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}");
            return match.Success ? match.Value.ToLowerInvariant() : "";
        }
    }

    public string SenderDomain
    {
        get
        {
            var senderEmail = SenderEmail;
            var atIndex = senderEmail.IndexOf('@');
            return atIndex >= 0 ? senderEmail[(atIndex + 1)..] : "";
        }
    }
}
