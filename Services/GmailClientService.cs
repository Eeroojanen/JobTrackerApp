using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace JobTracker.Services;

public class GmailClientService
{
    private static readonly string[] Scopes = { GmailService.Scope.GmailReadonly };
    private readonly string _appDataFolder;

    public GmailClientService()
    {
        _appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JobTracker");
    }

    public string CredentialsHint =>
        $"Place gmail-credentials.json in {_appDataFolder} or next to JobTracker.exe.";

    public async Task<GmailMessagePage> GetMessagePageAsync(
        string query,
        int pageSize,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var service = await CreateServiceAsync(cancellationToken);

        var listRequest = service.Users.Messages.List("me");
        listRequest.Q = BuildEffectiveQuery(query);
        listRequest.MaxResults = pageSize;
        listRequest.PageToken = pageToken;

        var listResponse = await listRequest.ExecuteAsync(cancellationToken);
        if (listResponse.Messages is null || listResponse.Messages.Count == 0)
            return new GmailMessagePage(Array.Empty<GmailMessage>(), listResponse.NextPageToken);

        var messages = new List<GmailMessage>();
        foreach (var item in listResponse.Messages)
        {
            var getRequest = service.Users.Messages.Get("me", item.Id);
            getRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;
            var fullMessage = await getRequest.ExecuteAsync(cancellationToken);
            messages.Add(ToMessage(fullMessage));
        }

        return new GmailMessagePage(
            messages.OrderByDescending(x => x.ReceivedAt).ToList(),
            listResponse.NextPageToken);
    }

    private static string BuildEffectiveQuery(string query)
    {
        var baseQuery = string.IsNullOrWhiteSpace(query) ? "newer_than:90d" : query.Trim();
        if (baseQuery.Contains("is:important", StringComparison.OrdinalIgnoreCase))
            return baseQuery;

        return $"is:important ({baseQuery})";
    }

    public async Task<IReadOnlyList<GmailMessage>> GetThreadMessagesAsync(
        string threadId,
        int maxMessages = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            return Array.Empty<GmailMessage>();

        var service = await CreateServiceAsync(cancellationToken);
        var thread = await service.Users.Threads.Get("me", threadId).ExecuteAsync(cancellationToken);
        if (thread.Messages is null || thread.Messages.Count == 0)
            return Array.Empty<GmailMessage>();

        return thread.Messages
            .Select(ToMessage)
            .OrderByDescending(x => x.ReceivedAt)
            .Take(Math.Max(1, maxMessages))
            .OrderBy(x => x.ReceivedAt)
            .ToList();
    }

    private async Task<GmailService> CreateServiceAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_appDataFolder);
        var credentialsPath = ResolveCredentialsPath();
        if (credentialsPath is null)
            throw new FileNotFoundException(CredentialsHint);

        await using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);
        var secrets = GoogleClientSecrets.FromStream(stream).Secrets;

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            Scopes,
            "jobtracker-user",
            cancellationToken,
            new FileDataStore(Path.Combine(_appDataFolder, "gmail-token"), true));

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "JobTracker"
        });
    }

    private string? ResolveCredentialsPath()
    {
        var localPath = Path.Combine(_appDataFolder, "gmail-credentials.json");
        if (File.Exists(localPath))
            return localPath;

        var appPath = Path.Combine(AppContext.BaseDirectory, "gmail-credentials.json");
        if (File.Exists(appPath))
            return appPath;

        return null;
    }

    private static GmailMessage ToMessage(Message message)
    {
        var subject = GetHeader(message.Payload, "Subject");
        var from = GetHeader(message.Payload, "From");
        var body = ExtractBody(message.Payload);
        var receivedAt = message.InternalDate.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds((long)message.InternalDate.Value).LocalDateTime
            : DateTime.Now;

        return new GmailMessage
        {
            Id = message.Id ?? "",
            ThreadId = message.ThreadId ?? "",
            Subject = subject,
            From = from,
            Snippet = message.Snippet ?? "",
            BodyText = body,
            ReceivedAt = receivedAt
        };
    }

    private static string GetHeader(MessagePart? payload, string name)
    {
        return payload?.Headers?.FirstOrDefault(h =>
            string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
    }

    private static string ExtractBody(MessagePart? payload)
    {
        if (payload is null)
            return "";

        if (!string.IsNullOrWhiteSpace(payload.Body?.Data))
            return DecodeBody(payload.Body.Data, payload.MimeType);

        if (payload.Parts is null || payload.Parts.Count == 0)
            return "";

        var plainTextPart = payload.Parts.FirstOrDefault(part =>
            string.Equals(part.MimeType, "text/plain", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(plainTextPart?.Body?.Data))
            return DecodeBody(plainTextPart.Body.Data, plainTextPart.MimeType);

        var htmlPart = payload.Parts.FirstOrDefault(part =>
            string.Equals(part.MimeType, "text/html", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(htmlPart?.Body?.Data))
            return DecodeBody(htmlPart.Body.Data, htmlPart.MimeType);

        foreach (var part in payload.Parts)
        {
            var nested = ExtractBody(part);
            if (!string.IsNullOrWhiteSpace(nested))
                return nested;
        }

        return "";
    }

    private static string DecodeBody(string data, string? mimeType)
    {
        var normalized = data.Replace('-', '+').Replace('_', '/');
        switch (normalized.Length % 4)
        {
            case 2:
                normalized += "==";
                break;
            case 3:
                normalized += "=";
                break;
        }

        var bytes = Convert.FromBase64String(normalized);
        var text = Encoding.UTF8.GetString(bytes);

        if (string.Equals(mimeType, "text/html", StringComparison.OrdinalIgnoreCase))
            return Regex.Replace(text, "<.*?>", " ");

        return text;
    }
}
