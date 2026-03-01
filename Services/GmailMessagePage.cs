using System.Collections.Generic;

namespace JobTracker.Services;

public sealed record GmailMessagePage(
    IReadOnlyList<GmailMessage> Messages,
    string? NextPageToken);
