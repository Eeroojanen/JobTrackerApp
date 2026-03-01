using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JobTracker.Models;

namespace JobTracker.Services;

public sealed class JobInboxAgent
{
    private readonly ApplicationMatcherService _retrieval;
    private readonly IJobInboxAgentModel _model;

    public JobInboxAgent(ApplicationMatcherService retrieval, IJobInboxAgentModel model)
    {
        _retrieval = retrieval;
        _model = model;
    }

    public bool IsConfigured => _model.IsConfigured;
    public string ConfigurationHint => _model.ConfigurationHint;

    public async Task<JobInboxAgentResult> ReviewAsync(
        IList<JobApplication> applications,
        GmailMessage message,
        IReadOnlyList<GmailMessage>? threadMessages = null,
        CancellationToken cancellationToken = default)
    {
        var rankedCandidates = _retrieval.RankCandidates(applications, message, maxCandidates: 6);
        var request = new JobInboxAgentRequest
        {
            Email = message,
            ThreadMessages = (threadMessages ?? new[] { message })
                .Select(ToEmailContext)
                .ToArray(),
            Candidates = rankedCandidates
                .Select(match => new JobInboxAgentCandidate
                {
                    Id = match.Application.Id,
                    CompanyName = match.Application.CompanyName,
                    Status = match.Application.Status,
                    Aliases = match.Application.CompanyAliases.ToArray(),
                    KnownSenderEmails = match.Application.KnownSenderEmails.ToArray(),
                    KnownSenderDomains = match.Application.KnownSenderDomains.ToArray(),
                    RecentMatchedEmails = match.Application.RecentMatchedEmails
                        .OrderByDescending(item => item.ReceivedAt)
                        .Take(5)
                        .OrderBy(item => item.ReceivedAt)
                        .Select(item => new JobInboxAgentCandidateEmailHistory
                        {
                            MessageId = item.MessageId,
                            ThreadId = item.ThreadId,
                            Subject = item.Subject,
                            Snippet = item.Snippet,
                            SenderEmail = item.SenderEmail,
                            ReceivedAt = item.ReceivedAt,
                            StatusAtTime = item.StatusAtTime
                        })
                        .ToArray(),
                    RetrievalConfidence = match.Confidence,
                    RetrievalReason = match.Reason
                })
                .ToArray()
        };

        var decision = await _model.DecideAsync(request, cancellationToken);
        var application = decision.ApplicationId.HasValue
            ? applications.FirstOrDefault(item => item.Id == decision.ApplicationId.Value)
            : null;

        return new JobInboxAgentResult
        {
            Decision = decision,
            Application = application
        };
    }

    private static JobInboxAgentEmailContext ToEmailContext(GmailMessage message)
    {
        return new JobInboxAgentEmailContext
        {
            MessageId = message.Id,
            ThreadId = message.ThreadId,
            From = message.From,
            SenderEmail = message.SenderEmail,
            SenderDomain = message.SenderDomain,
            Subject = message.Subject,
            Snippet = message.Snippet,
            BodyText = message.BodyText,
            ReceivedAt = message.ReceivedAt
        };
    }
}
