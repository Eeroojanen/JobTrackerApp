using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JobTracker.Models;

namespace JobTracker.Services;

public sealed class OpenAiJobInboxAgentModel : IJobInboxAgentModel
{
    private const string DefaultModel = "gpt-4.1-mini";
    private static readonly HttpClient HttpClient = new();
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly Uri _endpoint;

    public OpenAiJobInboxAgentModel()
    {
        _apiKey = Environment.GetEnvironmentVariable("JOBTRACKER_OPENAI_API_KEY")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        _model = Environment.GetEnvironmentVariable("JOBTRACKER_OPENAI_MODEL")
            ?? DefaultModel;

        var baseUrl = Environment.GetEnvironmentVariable("JOBTRACKER_OPENAI_BASE_URL")
            ?? "https://api.openai.com/v1/";
        _endpoint = new Uri(new Uri(EnsureTrailingSlash(baseUrl)), "chat/completions");
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public string ConfigurationHint =>
        "Set OPENAI_API_KEY (or JOBTRACKER_OPENAI_API_KEY) and optionally JOBTRACKER_OPENAI_MODEL.";

    public async Task<JobInboxAgentDecision> DecideAsync(
        JobInboxAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(ConfigurationHint);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(BuildPayload(request), JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = ExtractContent(body);
        return ParseDecision(content, body, request);
    }

    private object BuildPayload(JobInboxAgentRequest request)
    {
        var candidates = request.Candidates.Select(candidate => new
        {
            id = candidate.Id,
            company_name = candidate.CompanyName,
            status = candidate.Status.ToString(),
            aliases = candidate.Aliases,
            known_sender_emails = candidate.KnownSenderEmails,
            known_sender_domains = candidate.KnownSenderDomains,
            recent_matched_emails = candidate.RecentMatchedEmails.Select(item => new
            {
                message_id = item.MessageId,
                thread_id = item.ThreadId,
                subject = item.Subject,
                snippet = TrimText(item.Snippet, 400),
                sender_email = item.SenderEmail,
                received_at = item.ReceivedAt.ToString("O", CultureInfo.InvariantCulture),
                status_at_time = item.StatusAtTime.ToString()
            }),
            retrieval_confidence = candidate.RetrievalConfidence,
            retrieval_reason = candidate.RetrievalReason
        });

        var prompt = new
        {
            email = new
            {
                from = request.Email.From,
                sender_email = request.Email.SenderEmail,
                sender_domain = request.Email.SenderDomain,
                subject = request.Email.Subject,
                snippet = TrimText(request.Email.Snippet, 500),
                body = TrimText(request.Email.BodyText, 2500),
                received_at = request.Email.ReceivedAt.ToString("O", CultureInfo.InvariantCulture)
            },
            thread_messages = request.ThreadMessages.Select(message => new
            {
                message_id = message.MessageId,
                thread_id = message.ThreadId,
                from = message.From,
                sender_email = message.SenderEmail,
                sender_domain = message.SenderDomain,
                subject = message.Subject,
                snippet = TrimText(message.Snippet, 400),
                body = TrimText(message.BodyText, 1200),
                received_at = message.ReceivedAt.ToString("O", CultureInfo.InvariantCulture)
            }),
            candidate_applications = candidates,
            instructions = new[]
            {
                "You are a job application inbox agent.",
                "Decide if the email is related to a job application process.",
                "Treat LinkedIn notifications, LinkedIn Job Alerts, job board digests, and promoted role emails as not job-related unless the message is clearly a direct recruiter or hiring-process email.",
                "Use the thread_messages as the primary conversation history for the current email.",
                "Use recent_matched_emails under each candidate to understand the prior application timeline and past recruiter messages.",
                "Use the candidate applications only when selecting application_id. If none fit, return null.",
                "Be conservative with newsletters, promotions, discount offers, signatures, and footer text.",
                "Make the status decision from the latest message in context, but use earlier thread messages to resolve ambiguity.",
                "Only suggest a status when the conversation clearly changes or confirms the application state.",
                "Valid statuses are Pending, Proceed, Rejected, or null.",
                "Set needs_human_review to true when evidence is weak, mixed, or ambiguous.",
                "Return JSON only."
            },
            response_schema = new
            {
                is_job_related = "boolean",
                application_id = "guid or null",
                suggested_status = "Pending | Proceed | Rejected | null",
                confidence = "0.0 to 1.0",
                needs_human_review = "boolean",
                reason = "short string"
            }
        };

        return new
        {
            model = _model,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You must return a single JSON object with no markdown and no extra text."
                },
                new
                {
                    role = "user",
                    content = JsonSerializer.Serialize(prompt, JsonOptions)
                }
            },
            temperature = 0.1
        };
    }

    private static string ExtractContent(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var choices = root.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
            throw new InvalidOperationException("The AI agent returned no choices.");

        var message = choices[0].GetProperty("message");
        if (!message.TryGetProperty("content", out var contentElement))
            throw new InvalidOperationException("The AI agent response did not contain content.");

        return contentElement.GetString() ?? "";
    }

    private static JobInboxAgentDecision ParseDecision(
        string content,
        string rawResponse,
        JobInboxAgentRequest request)
    {
        var json = ExtractJsonObject(content);
        var payload = JsonSerializer.Deserialize<DecisionPayload>(json, JsonOptions)
            ?? throw new InvalidOperationException("The AI agent returned invalid JSON.");

        var applicationId = ParseApplicationId(payload.ApplicationId, request);
        var confidence = Math.Clamp(payload.Confidence ?? 0, 0, 1);

        return new JobInboxAgentDecision
        {
            IsJobRelated = payload.IsJobRelated ?? false,
            ApplicationId = applicationId,
            SuggestedStatus = ParseStatus(payload.SuggestedStatus),
            Confidence = confidence,
            NeedsHumanReview = payload.NeedsHumanReview ?? true,
            Reason = (payload.Reason ?? "").Trim(),
            RawResponse = rawResponse
        };
    }

    private static Guid? ParseApplicationId(string? value, JobInboxAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!Guid.TryParse(value, out var applicationId))
            return null;

        return request.Candidates.Any(candidate => candidate.Id == applicationId)
            ? applicationId
            : null;
    }

    private static ApplicationStatus? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Enum.TryParse<ApplicationStatus>(value, true, out var status)
            ? status
            : null;
    }

    private static string ExtractJsonObject(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
                return trimmed[firstBrace..(lastBrace + 1)];
        }

        return trimmed;
    }

    private static string TrimText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : $"{trimmed[..maxLength]}...";
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
    }

    private sealed class DecisionPayload
    {
        [JsonPropertyName("is_job_related")]
        public bool? IsJobRelated { get; init; }

        [JsonPropertyName("application_id")]
        public string? ApplicationId { get; init; }

        [JsonPropertyName("suggested_status")]
        public string? SuggestedStatus { get; init; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; init; }

        [JsonPropertyName("needs_human_review")]
        public bool? NeedsHumanReview { get; init; }

        [JsonPropertyName("reason")]
        public string? Reason { get; init; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };
}
