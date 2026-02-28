using System;

namespace JobTracker.Models;

public class JobApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CompanyName { get; set; } = "";
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}