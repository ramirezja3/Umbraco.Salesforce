# Umbraco Automate Salesforce

Salesforce connection and actions for [Umbraco Automate](https://github.com/umbraco/Umbraco.Automate).

## Overview

Umbraco.Community.Automate.Salesforce is a provider package that adds Salesforce connectivity to Umbraco Automate. It contributes a Salesforce connection type (authenticated via OAuth) and six named CRM actions usable as steps in automations — for example, creating a Lead when content is published.

Requires **Umbraco.Automate** (core) already installed and composed — this package does nothing on its own. `Umbraco.Automate.OpenIddict` is pulled in automatically as a transitive dependency; you never install it by hand.

## Key Features

- **Salesforce connection type** — OAuth-based, for production orgs (`login.salesforce.com`), managed in the backoffice
- **Six CRM actions** — Create Lead, Create/Update Contact, Create Opportunity, Update Opportunity Stage, Add to Campaign, Log Engagement Activity
- **Automatic token management** — OAuth credentials are stored and refreshed transparently
- **Rate-limit aware**

## Installation

```bash
dotnet add package Umbraco.Community.Automate.Salesforce
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

For the full Connected App setup walkthrough, see the **Documentation** section below.

## Documentation

Full documentation lives in the [GitHub repository](https://github.com/ramirezja3/Umbraco.Salesforce).

## License

MIT - see the [GitHub repository](https://github.com/ramirezja3/Umbraco.Salesforce) for the full license text.
