using System;
using System.Collections.Generic;

namespace JobTracker.Models;

public class JobApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CompanyName { get; set; } = "";
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? LastEmailSubject { get; set; }
    public DateTime? LastEmailReceivedAt { get; set; }
    public string? LastEmailMessageId { get; set; }
    public double? ClassifierConfidence { get; set; }
    public string? ClassifierReason { get; set; }
    public List<string> CompanyAliases { get; set; } = new();
    public List<string> KnownSenderEmails { get; set; } = new();
    public List<string> KnownSenderDomains { get; set; } = new();
    public List<ApplicationEmailHistoryEntry> RecentMatchedEmails { get; set; } = new();
}
