using System;

namespace JobTracker.Models;

public class ApplicationEmailHistoryEntry
{
    public string MessageId { get; set; } = "";
    public string ThreadId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Snippet { get; set; } = "";
    public string SenderEmail { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
    public ApplicationStatus StatusAtTime { get; set; }
}
