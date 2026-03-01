using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JobTracker.Models;

namespace JobTracker.Services;

public class GmailSyncService
{
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

                var review = await _agent.ReviewAsync(applications, message, cancellationToken);
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
}
