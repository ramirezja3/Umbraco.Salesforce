# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **Note:** This is the Umbraco.Automate.Salesforce package — a provider that adds Salesforce connectivity to [Umbraco Automate](https://github.com/umbraco/Umbraco.Automate), mirroring the structure and conventions of `Umbraco.Automate.Slack` and `Umbraco.Automate.OpenIddict`. For the full historical design/build log — including non-obvious platform constraints that took real investigation to uncover — see `docs/dev-notes.md`.

## Build Commands

```bash
# Build the solution
dotnet build Umbraco.Automate.Salesforce.slnx

# Run unit tests
dotnet test tests/Umbraco.Automate.Salesforce.Tests.Unit/Umbraco.Automate.Salesforce.Tests.Unit.csproj

# Pack all 4 projects with real (non-project-reference) dependency pins, for release
./scripts/pack-release.ps1
```

## Architecture Overview

Umbraco.Automate.Salesforce is a provider package that adds Salesforce connectivity to Umbraco Automate. It uses `Umbraco.Automate.OpenIddict` for OAuth authentication and provides Salesforce connection types, actions, and one polling-based trigger.

Unlike Slack (a single RCL project), this package needs its own persistence — a polling-trigger checkpoint table — so it's split the way `Umbraco.Automate.OpenIddict` is split, not the way Slack is:

### Project Structure

```
Umbraco.Automate.Salesforce/
├── src/
│   ├── Umbraco.Automate.Salesforce/                        # Meta-package (bundles the three below)
│   ├── Umbraco.Automate.Salesforce.Core/                    # Actions, Triggers, Connection, Configuration
│   ├── Umbraco.Automate.Salesforce.Persistence.SqlServer/   # EF Core migrations (SQL Server)
│   └── Umbraco.Automate.Salesforce.Persistence.Sqlite/      # EF Core migrations (SQLite)
├── tests/
│   ├── Umbraco.Automate.Salesforce.Tests.Unit/
│   └── Umbraco.Automate.Salesforce.Tests.Integration/
└── Umbraco.Automate.Salesforce.slnx
```

### How It Works

1. `SalesforceComposer` registers two OpenIddict Client WebIntegration registrations — `Salesforce` (production, `login.salesforce.com`) and `SalesforceSandbox` (`test.salesforce.com`) — plus this package's own services and EF Core `DbContext`.
2. `SalesforceConnectionType` / `SalesforceSandboxConnectionType` define the two connection types using `OAuthConnectionTypeBase`. Two types exist (not one, parameterized) because an OpenIddict Client registration's issuer is fixed per provider name at startup.
3. `SalesforceConnectionSettings` holds the `OAuthCredentialsId` linking to stored tokens; the organization's `instance_url` (Salesforce's non-standard OAuth token-response field) is stashed in the credential's generic `AccountLabel` column rather than a new table.
4. Actions (Create Lead, Create/Update/Upsert/Get/Delete Record, Query Records) resolve a live access token + instance URL via `ISalesforceConnectionResolver` and call the Salesforce REST API through `ISalesforceClient`, which handles rate-limit backoff and a stale-session refresh-and-retry-once.
5. The one trigger (Opportunity Stage Changed) is polling-based, run by `SalesforcePollingBackgroundJob` on a per-automation checkpoint persisted via `SalesforceDbContext`.

### Configuration

Provider credentials are configured via `appsettings.json`:

```json
{
  "Umbraco": {
    "Automate": {
      "Providers": {
        "Salesforce": {
          "ClientId": "your-connected-app-consumer-key",
          "ClientSecret": "your-connected-app-consumer-secret"
        },
        "SalesforceSandbox": {
          "ClientId": "your-connected-app-consumer-key",
          "ClientSecret": "your-connected-app-consumer-secret"
        }
      },
      "Salesforce": {
        "ApiVersion": "v62.0"
      }
    }
  }
}
```

`Umbraco:Automate:Providers:Salesforce`/`:SalesforceSandbox` are the OAuth app credentials (bound generically by `Umbraco.Automate.OpenIddict`); `Umbraco:Automate:Salesforce` is this package's own REST API behavior (API version, query row caps, retry/backoff, polling cadence — see `SalesforceApiOptions`/`SalesforcePollingOptions`).

## Dependencies

- Umbraco.Automate.Core
- Umbraco.Automate.OpenIddict
- OpenIddict.Client.WebIntegration 7.4.x
- Umbraco.Cms.Persistence.EFCore (+ .SqlServer / .Sqlite)

## Commit Scopes

Use these scopes for conventional commits affecting this package:

`salesforce`
