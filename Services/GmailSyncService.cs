using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JobTracker.Models;

namespace JobTracker.Services;

public class GmailSyncService
{
    private static readonly string[] IgnoredSenderDomains =
    {
        "linkedin.com",
        "linkedinmail.com"
    };

    private const int ThreadContextMessageCount = 8;
    private const int RecentMatchedEmailHistoryLimit = 8;
    private readonly GmailClientService _gmailClientService = new();
    private readonly ApplicationMatcherService _matcher = new();
    private readonly JobInboxAgent _agent;
    private const int PageSize = 50;
    private const int MaxMessagesToScan = 250;
    private const int MaxMatchedMessagesToProcess = 25;
    private const double MinimumAutoAttachConfidence = 0.72;
    private const double MinimumAutoUpdateDecisionConfidence = 0.80;

    public GmailSyncService()
    {
        _agent = new JobInboxAgent(_matcher, new OpenAiJobInboxAgentModel());
    }

    public string CredentialsHint => $"{_gmailClientService.CredentialsHint} {_agent.ConfigurationHint}";

    public async Task<string> SyncAsync(IList<JobApplication> applications, string query, CancellationToken cancellationToken = default)
    {
        if (!_agent.IsConfigured)
            return $"AI agent is not configured. {_agent.ConfigurationHint}";

        var scanned = 0;
        var reviewed = 0;
        var matched = 0;
        var updated = 0;
        var needsReview = 0;
        var pageCount = 0;
        string? nextPageToken = null;

        while (scanned < MaxMessagesToScan && matched < MaxMatchedMessagesToProcess)
        {
            var page = await _gmailClientService.GetMessagePageAsync(query, PageSize, nextPageToken, cancellationToken);
            pageCount++;

            if (page.Messages.Count == 0)
                break;

            foreach (var message in page.Messages)
            {
                if (scanned >= MaxMessagesToScan || matched >= MaxMatchedMessagesToProcess)
                    break;

                scanned++;

                if (applications.Any(application => application.LastEmailMessageId == message.Id))
                    continue;

                if (ShouldIgnoreMessage(message))
                    continue;

                var threadMessages = await _gmailClientService.GetThreadMessagesAsync(
                    message.ThreadId,
                    ThreadContextMessageCount,
                    cancellationToken);

                var review = await _agent.ReviewAsync(applications, message, threadMessages, cancellationToken);
                reviewed++;

                if (!review.Decision.IsJobRelated)
                    continue;

                if (review.Application is null)
                {
                    if (review.Decision.NeedsHumanReview)
                        needsReview++;
                    continue;
                }

                matched++;
                var application = review.Application;
                application.ClassifierConfidence = review.Decision.Confidence;
                application.ClassifierReason = $"AI Agent: {review.Decision.Reason}";

                if (review.Decision.NeedsHumanReview || review.Decision.Confidence < MinimumAutoAttachConfidence)
                {
                    needsReview++;
                    continue;
                }

                application.LastEmailMessageId = message.Id;
                application.LastEmailSubject = message.Subject;
                application.LastEmailReceivedAt = message.ReceivedAt;
                application.ClassifierReason =
                    $"AI Agent: {review.Decision.Reason} Match: {review.Application.CompanyName}";
                LearnKnownSenders(application, message);

                if (review.Decision.SuggestedStatus.HasValue &&
                    review.Decision.Confidence >= MinimumAutoUpdateDecisionConfidence &&
                    (review.Decision.SuggestedStatus != ApplicationStatus.Pending || application.Status == ApplicationStatus.Pending) &&
                    application.Status != review.Decision.SuggestedStatus.Value)
                {
                    application.Status = review.Decision.SuggestedStatus.Value;
                    updated++;
                }

                RecordRecentMatchedEmail(application, message);
            }

            nextPageToken = page.NextPageToken;
            if (string.IsNullOrWhiteSpace(nextPageToken))
                break;
        }

        return $"Gmail sync scanned {scanned} messages across {pageCount} pages, reviewed {reviewed}, matched {matched}, updated {updated}, needs review {needsReview}.";
    }

    private static void LearnKnownSenders(JobApplication application, GmailMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.SenderEmail) &&
            !application.KnownSenderEmails.Any(value => string.Equals(value, message.SenderEmail, System.StringComparison.OrdinalIgnoreCase)))
        {
            application.KnownSenderEmails.Add(message.SenderEmail);
        }

        if (!string.IsNullOrWhiteSpace(message.SenderDomain) &&
            !application.KnownSenderDomains.Any(value => string.Equals(value, message.SenderDomain, System.StringComparison.OrdinalIgnoreCase)))
        {
            application.KnownSenderDomains.Add(message.SenderDomain);
        }
    }

    private static void RecordRecentMatchedEmail(JobApplication application, GmailMessage message)
    {
        application.RecentMatchedEmails.RemoveAll(item => item.MessageId == message.Id);
        application.RecentMatchedEmails.Insert(0, new ApplicationEmailHistoryEntry
        {
            MessageId = message.Id,
            ThreadId = message.ThreadId,
            Subject = message.Subject,
            Snippet = message.Snippet,
            SenderEmail = message.SenderEmail,
            ReceivedAt = message.ReceivedAt,
            StatusAtTime = application.Status
        });

        if (application.RecentMatchedEmails.Count > RecentMatchedEmailHistoryLimit)
        {
            application.RecentMatchedEmails.RemoveRange(
                RecentMatchedEmailHistoryLimit,
                application.RecentMatchedEmails.Count - RecentMatchedEmailHistoryLimit);
        }
    }

    private static bool ShouldIgnoreMessage(GmailMessage message)
    {
        var senderDomain = (message.SenderDomain ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(senderDomain) &&
            IgnoredSenderDomains.Any(domain =>
                senderDomain.Equals(domain, System.StringComparison.OrdinalIgnoreCase) ||
                senderDomain.EndsWith($".{domain}", System.StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var subject = (message.Subject ?? "").Trim();
        return subject.Contains("linkedin job alert", System.StringComparison.OrdinalIgnoreCase) ||
               subject.Contains("linkedin alert", System.StringComparison.OrdinalIgnoreCase);
    }
}
