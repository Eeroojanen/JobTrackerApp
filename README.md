# JobTracker

Desktop app for tracking job applications and reviewing Gmail replies with an AI inbox agent.

## Features

- Track companies and application status
- Import applications from Excel
- Sync Gmail replies with the Gmail API
- Use an AI agent to decide whether an email is job-related
- Match the email to the correct application from a ranked shortlist
- Suggest or auto-apply status changes for clear recruiter responses

## Tech overview

The current Gmail flow is agent-based:

1. `GmailClientService` reads messages from Gmail.
2. `ApplicationMatcherService` retrieves the top candidate applications.
3. `JobInboxAgent` sends the email and candidate shortlist to the model.
4. `OpenAiJobInboxAgentModel` returns a structured JSON decision.
5. `GmailSyncService` validates that decision and updates the local application list only when confidence is high enough.

If the AI agent is not configured, Gmail sync will not perform matching.

## Build and run

```bash
dotnet run
```

## Publish

Build a portable Windows x64 release:

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Published output:

```text
bin\Release\net8.0\win-x64\publish\
```

## Desktop shortcut

After publishing or unzipping a release build, run:

```powershell
./scripts/create-desktop-shortcut.ps1
```

This creates a `JobTracker` shortcut on the desktop.

## Application usage

1. Add companies manually with a status.
2. Search and filter the current list.
3. Remove selected applications or clear the list.
4. Import rows from Excel by mapping company and status columns.
5. Sync Gmail to let the AI inbox agent review recent messages.

## Gmail setup

1. Create or open a Google Cloud project.
2. Enable the Gmail API.
3. Create an OAuth client of type `Desktop app`.
4. Download the OAuth client JSON.
5. Rename it to `gmail-credentials.json`.
6. Place it in either:
   - `%LOCALAPPDATA%\JobTracker\gmail-credentials.json`
   - next to `JobTracker.exe`
7. Start the app and use `Sync Gmail`.
8. On first use, complete the Google consent flow in the browser.

Suggested Gmail query:

```text
newer_than:90d (application OR interview OR recruiter OR hiring)
```

## OpenAI setup

The AI agent needs an API key.

Create a key from:

```text
https://platform.openai.com/api-keys
```

Set one of these environment variables:

```powershell
$env:OPENAI_API_KEY="your_api_key"
```

or

```powershell
$env:JOBTRACKER_OPENAI_API_KEY="your_api_key"
```

Optional model override:

```powershell
$env:JOBTRACKER_OPENAI_MODEL="gpt-4.1-mini"
```

Optional base URL override for an OpenAI-compatible endpoint:

```powershell
$env:JOBTRACKER_OPENAI_BASE_URL="https://api.openai.com/v1/"
```

To persist the variables for your Windows user profile:

```powershell
[Environment]::SetEnvironmentVariable("OPENAI_API_KEY", "your_api_key", "User")
[Environment]::SetEnvironmentVariable("JOBTRACKER_OPENAI_MODEL", "gpt-4.1-mini", "User")
```

Restart the app after changing environment variables.

## Gmail sync behavior

- The app scans recent Gmail messages using your query.
- The AI agent decides whether each email is job-related.
- The app only accepts an application match if the model returns one of the shortlisted application IDs.
- Low-confidence or ambiguous decisions are counted as needing review.
- High-confidence decisions can attach the message to an application automatically.
- High-confidence status changes can update the application status automatically.
- The latest matched email subject, timestamp, and agent reason are stored on the application.

## Notes

- Gmail sync requires valid Google OAuth credentials.
- AI matching requires an OpenAI API key.
- Current build warnings about `OpenFileDialog` in Avalonia are unrelated to Gmail sync.
