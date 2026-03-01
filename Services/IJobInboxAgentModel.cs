using System.Threading;
using System.Threading.Tasks;

namespace JobTracker.Services;

public interface IJobInboxAgentModel
{
    bool IsConfigured { get; }
    string ConfigurationHint { get; }
    Task<JobInboxAgentDecision> DecideAsync(JobInboxAgentRequest request, CancellationToken cancellationToken = default);
}
