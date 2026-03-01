using System;
using System.Collections.Generic;
using JobTracker.Models;

namespace JobTracker.Services;

public sealed class JobInboxAgentRequest
{
    public GmailMessage Email { get; init; } = new();
    public IReadOnlyList<JobInboxAgentEmailContext> ThreadMessages { get; init; } = Array.Empty<JobInboxAgentEmailContext>();
    public IReadOnlyList<JobInboxAgentCandidate> Candidates { get; init; } = Array.Empty<JobInboxAgentCandidate>();
}

public sealed class JobInboxAgentEmailContext
{
    public string MessageId { get; init; } = "";
    public string ThreadId { get; init; } = "";
    public string From { get; init; } = "";
    public string SenderEmail { get; init; } = "";
    public string SenderDomain { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Snippet { get; init; } = "";
    public string BodyText { get; init; } = "";
    public DateTime ReceivedAt { get; init; }
}

public sealed class JobInboxAgentCandidateEmailHistory
{
    public string MessageId { get; init; } = "";
    public string ThreadId { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Snippet { get; init; } = "";
    public string SenderEmail { get; init; } = "";
    public DateTime ReceivedAt { get; init; }
    public ApplicationStatus StatusAtTime { get; init; }
}

public sealed class JobInboxAgentCandidate
{
    public Guid Id { get; init; }
    public string CompanyName { get; init; } = "";
    public ApplicationStatus Status { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> KnownSenderEmails { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> KnownSenderDomains { get; init; } = Array.Empty<string>();
    public IReadOnlyList<JobInboxAgentCandidateEmailHistory> RecentMatchedEmails { get; init; } = Array.Empty<JobInboxAgentCandidateEmailHistory>();
    public double RetrievalConfidence { get; init; }
    public string RetrievalReason { get; init; } = "";
}

public sealed class JobInboxAgentDecision
{
    public bool IsJobRelated { get; init; }
    public Guid? ApplicationId { get; init; }
    public ApplicationStatus? SuggestedStatus { get; init; }
    public double Confidence { get; init; }
    public bool NeedsHumanReview { get; init; }
    public string Reason { get; init; } = "";
    public string RawResponse { get; init; } = "";
}

public sealed class JobInboxAgentResult
{
    public JobInboxAgentDecision Decision { get; init; } = new();
    public JobApplication? Application { get; init; }
}
