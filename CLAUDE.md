# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **Note:** This is the Umbraco.Automate.Salesforce package — a provider that adds Salesforce connectivity to [Umbraco Automate](https://github.com/umbraco/Umbraco.Automate), mirroring the structure and conventions of `Umbraco.Automate.Slack` and `Umbraco.Automate.OpenIddict`. For the full historical design/build log — including non-obvious platform constraints that took real investigation to uncover — see `docs/dev-notes.md`.

## Build Commands

```bash
# Build the solution
dotnet build Umbraco.Automate.Salesforce.slnx

# Run unit tests
dotnet test tests/Umbraco.Automate.Salesforce.Tests.Unit/Umbraco.Automate.Salesforce.Tests.Unit.csproj

# Pack the package with real (non-project-reference) dependency pins, for release
./scripts/pack-release.ps1
```

## Architecture Overview

Umbraco.Automate.Salesforce is a provider package that adds Salesforce connectivity to Umbraco Automate. It uses `Umbraco.Automate.OpenIddict` for OAuth authentication and provides Salesforce connection types and six actions — no triggers (removed in v2; see `docs/dev-notes.md`). Each action targets one fixed, named Salesforce object with named fields (no generic "pick an object API name" action — see `docs/dev-notes.md`'s most recent entry for why). Every action calls the Salesforce REST API directly and keeps no local state, so this package needs no persistence of its own — it's a single Razor SDK RCL, the same shape as `Umbraco.Automate.Slack`, not split the way `Umbraco.Automate.OpenIddict` is.

### Project Structure

```
Umbraco.Automate.Salesforce/
├── src/
│   └── Umbraco.Automate.Salesforce/    # Actions, Connection, Configuration — the one NuGet package
├── tests/
│   ├── Umbraco.Automate.Salesforce.Tests.Unit/
│   └── Umbraco.Automate.Salesforce.Tests.Integration/
└── Umbraco.Automate.Salesforce.slnx
```

### How It Works

1. `SalesforceComposer` registers two OpenIddict Client WebIntegration registrations — `Salesforce` (production, `login.salesforce.com`) and `SalesforceSandbox` (`test.salesforce.com`) — plus this package's own services.
2. `SalesforceConnectionType` / `SalesforceSandboxConnectionType` define the two connection types using `OAuthConnectionTypeBase`. Two types exist (not one, parameterized) because an OpenIddict Client registration's issuer is fixed per provider name at startup.
3. `SalesforceConnectionSettings` holds the `OAuthCredentialsId` linking to stored tokens; the organization's `instance_url` (Salesforce's non-standard OAuth token-response field) is stashed in the credential's generic `AccountLabel` column rather than a new table.
4. Actions (Create Lead, Create/Update Contact, Create Opportunity, Update Opportunity Stage, Add to Campaign, Log Engagement Activity) resolve a live access token + instance URL via `ISalesforceConnectionResolver` and call the Salesforce REST API through `ISalesforceClient`, which handles rate-limit backoff (capped by `MaxRetryDelay`), network-level failures, and a stale-session refresh-and-retry-once.

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

`Umbraco:Automate:Providers:Salesforce`/`:SalesforceSandbox` are the OAuth app credentials (bound generically by `Umbraco.Automate.OpenIddict`, including their `Scopes` array — see `SalesforceComposer.ResolveScopes`); `Umbraco:Automate:Salesforce` is this package's own REST API behavior (API version, query row cap, retry/backoff — see `SalesforceApiOptions`).

## Dependencies

- Umbraco.Automate.Core
- Umbraco.Automate.OpenIddict
- OpenIddict.Client.WebIntegration 7.4.x

## Commit Scopes

Use these scopes for conventional commits affecting this package:

`salesforce`
