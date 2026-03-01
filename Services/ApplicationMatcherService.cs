using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JobTracker.Models;

namespace JobTracker.Services;

public class ApplicationMatcherService
{
    public IReadOnlyList<ApplicationMatch> RankCandidates(
        IEnumerable<JobApplication> applications,
        GmailMessage message,
        int maxCandidates = 5)
    {
        var content = MatchContent.FromMessage(message);
        return applications
            .Select(application => new
            {
                Application = application,
                Match = Score(application, content, message)
            })
            .Where(x => x.Match.Score > 0)
            .OrderByDescending(x => x.Match.Score)
            .ThenByDescending(x => x.Application.CreatedAt)
            .Take(Math.Max(1, maxCandidates))
            .Select(x => ToRankedMatch(x.Application, x.Match))
            .ToList();
    }

    private static MatchResult Score(JobApplication application, MatchContent content, GmailMessage message)
    {
        var candidateNames = GetCandidateNames(application).ToList();
        if (candidateNames.Count == 0)
            return MatchResult.None;

        var score = 0;
        var reasons = new List<string>();

        foreach (var candidateName in candidateNames)
        {
            if (ContainsWholePhrase(content.Sender, candidateName))
            {
                score += candidateName == Normalize(application.CompanyName) ? 10 : 8;
                reasons.Add($"sender:{candidateName}");
            }

            if (ContainsWholePhrase(content.SubjectOrSnippet, candidateName))
            {
                score += candidateName == Normalize(application.CompanyName) ? 8 : 6;
                reasons.Add($"subject:{candidateName}");
            }

            if (ContainsWholePhrase(content.Body, candidateName))
            {
                score += 2;
                reasons.Add($"body:{candidateName}");
            }

            foreach (var token in GetMeaningfulTokens(candidateName))
            {
                if (ContainsWholeWord(content.Sender, token))
                {
                    score += 3;
                }

                if (ContainsWholeWord(content.SubjectOrSnippet, token))
                {
                    score += 2;
                }

                if (ContainsWholeWord(content.Body, token))
                    score += 1;
            }
        }

        if (!string.IsNullOrWhiteSpace(message.SenderEmail) &&
            application.KnownSenderEmails.Any(value => string.Equals(value, message.SenderEmail, StringComparison.OrdinalIgnoreCase)))
        {
            score += 12;
            reasons.Add("known-sender-email");
        }

        if (!string.IsNullOrWhiteSpace(message.SenderDomain) &&
            application.KnownSenderDomains.Any(value => string.Equals(value, message.SenderDomain, StringComparison.OrdinalIgnoreCase)))
        {
            score += 10;
            reasons.Add("known-sender-domain");
        }

        if (!string.IsNullOrWhiteSpace(message.SenderDomain))
        {
            var domainTokens = message.SenderDomain.Split('.', StringSplitOptions.RemoveEmptyEntries);
            foreach (var candidateName in candidateNames)
            {
                if (domainTokens.Any(token => string.Equals(token, candidateName, StringComparison.Ordinal)))
                {
                    score += 5;
                    reasons.Add($"domain:{candidateName}");
                }

                foreach (var token in GetMeaningfulTokens(candidateName))
                {
                    if (domainTokens.Any(domainToken => string.Equals(domainToken, token, StringComparison.Ordinal)))
                    {
                        score += 2;
                    }
                }
            }
        }

        return new MatchResult(score, string.Join(", ", reasons.Distinct()));
    }

    private static ApplicationMatch ToRankedMatch(JobApplication application, MatchResult match)
    {
        return new ApplicationMatch(
            application,
            Math.Min(match.Score / 14.0, 0.99),
            match.MatchReason);
    }

    private static IEnumerable<string> GetCandidateNames(JobApplication application)
    {
        return new[] { application.CompanyName }
            .Concat(application.CompanyAliases ?? Enumerable.Empty<string>())
            .Select(Normalize)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal);
    }

    private static IEnumerable<string> GetMeaningfulTokens(string company)
    {
        return company
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 3)
            .Where(token => !IgnoredTokens.Contains(token));
    }

    private static bool ContainsWholeWord(string haystack, string token)
    {
        return Regex.IsMatch(haystack, $@"\b{Regex.Escape(token)}\b");
    }

    private static bool ContainsWholePhrase(string haystack, string phrase)
    {
        return Regex.IsMatch(haystack, $@"\b{Regex.Escape(phrase)}\b");
    }

    private static string Normalize(string value)
    {
        var lower = value.ToLowerInvariant();
        return Regex.Replace(lower, "[^a-z0-9@. ]+", " ").Trim();
    }

    private static readonly HashSet<string> IgnoredTokens = new(StringComparer.Ordinal)
    {
        "inc",
        "llc",
        "ltd",
        "oy",
        "ab",
        "plc",
        "group",
        "company",
        "careers",
        "jobs",
        "team"
    };

    private readonly record struct MatchContent(
        string Sender,
        string SubjectOrSnippet,
        string Body)
    {
        public static MatchContent FromMessage(GmailMessage message)
        {
            return new MatchContent(
                Normalize($"{message.From}\n{message.SenderEmail}\n{message.SenderDomain}"),
                Normalize($"{message.Subject}\n{message.Snippet}"),
                Normalize(message.BodyText));
        }
    }

    private readonly record struct MatchResult(
        int Score,
        string MatchReason)
    {
        public static MatchResult None => new(0, "");
    }
}

public sealed record ApplicationMatch(
    JobApplication Application,
    double Confidence,
    string Reason);
