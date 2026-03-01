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
        CancellationToken cancellationToken = default)
    {
        var rankedCandidates = _retrieval.RankCandidates(applications, message, maxCandidates: 5);
        var request = new JobInboxAgentRequest
        {
            Email = message,
            Candidates = rankedCandidates
                .Select(match => new JobInboxAgentCandidate
                {
                    Id = match.Application.Id,
                    CompanyName = match.Application.CompanyName,
                    Status = match.Application.Status,
                    Aliases = match.Application.CompanyAliases.ToArray(),
                    KnownSenderEmails = match.Application.KnownSenderEmails.ToArray(),
                    KnownSenderDomains = match.Application.KnownSenderDomains.ToArray(),
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
}
