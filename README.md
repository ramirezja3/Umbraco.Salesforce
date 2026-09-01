# Umbraco Automate Salesforce

Salesforce connection and actions for [Umbraco Automate](https://github.com/umbraco/Umbraco.Automate).

## Overview

Umbraco.Automate.Salesforce is a provider package that adds Salesforce connectivity to Umbraco Automate. It contributes two Salesforce connection types (production and sandbox, authenticated via OAuth) and six named CRM actions usable as steps in automations — for example, creating a Lead when content is published.

Requires **Umbraco.Automate** (core) already installed and composed — this package does nothing on its own. `Umbraco.Automate.OpenIddict` is pulled in automatically as a transitive dependency; you never install it by hand.

## Key Features

- **Salesforce connection types** — OAuth-based, for production (`login.salesforce.com`) and sandbox (`test.salesforce.com`) orgs, managed in the backoffice
- **Six CRM actions** — Create Lead, Create/Update Contact, Create Opportunity, Update Opportunity Stage, Add to Campaign, Log Engagement Activity
- **Automatic token management** — OAuth credentials are stored and refreshed transparently
- **Rate-limit aware** — backs off automatically on Salesforce's `REQUEST_LIMIT_EXCEEDED` and 429 responses

## Installation

```bash
dotnet add package Umbraco.Automate.Salesforce
```

## Configuration

Create a Salesforce Connected App and configure its credentials via `appsettings.json`:

```json
{
  "Umbraco": {
    "Automate": {
      "Providers": {
        "Salesforce": {
          "ClientId": "your-connected-app-consumer-key",
          "ClientSecret": "your-connected-app-consumer-secret"
        }
      }
    }
  }
}
```

The OAuth callback URI follows the convention `{your-site}/umbraco/automate/oauth/callback/salesforce` — add it to your Connected App's callback URLs. Then create a Salesforce connection in a workspace from the backoffice and authorize it via the OAuth popup.

See [docs/installation.md](docs/installation.md) for the full Connected App setup walkthrough.

## Documentation

- [docs/installation.md](docs/installation.md) — Connected App setup, configuration, first connect
- [docs/actions.md](docs/actions.md) — the six actions, their inputs/outputs, and example use
- [docs/security.md](docs/security.md) — security and compliance posture
- [docs/troubleshooting.md](docs/troubleshooting.md) — common Salesforce error codes and what to do about each

## License

MIT - See [LICENSE](LICENSE) for details.
