# Installing Umbraco.Automate.Salesforce

## Step 0: Prerequisites

Before installing this package, you need:

1. **Umbraco CMS 17.x** running.
2. **Umbraco.Automate** (the core automation engine) installed and composed — this package is an add-on and does nothing without it. See Umbraco Automate's own installation documentation.

`Umbraco.Automate.OpenIddict` is *not* something you install yourself — it comes in automatically as a transitive dependency of this package.

## Step 1: Create a Salesforce Connected App

1. In Salesforce, go to **Setup → App Manager → New Connected App** (or **External Client App**, per Salesforce's current guidance — this has shifted over time, so check Salesforce's own current documentation if the menu looks different).
2. Enable **OAuth Settings**.
3. Set the **Callback URL** to:
   ```
   https://<your-site>/umbraco/automate/oauth/callback/salesforce
   ```
4. Under **Selected OAuth Scopes**, add at minimum:
   - `api` — access and manage data
   - `refresh_token` (or `offline_access`) — maintain a long-lived connection without asking the connected user to log in again
5. Enable **Require Proof Key for Code Exchange (PKCE)** if your Salesforce edition offers it.
6. Save, then note the **Consumer Key** (Client ID) and **Consumer Secret** (Client Secret) — Salesforce may take a few minutes to activate a newly created Connected App.

If you're connecting to a **sandbox** org instead of production, everything above is identical — just remember which one you're configuring, since sandbox and production use separate connection types (see below).

## Step 2: Add the package

```bash
dotnet add package Umbraco.Automate.Salesforce
```

This pulls in `Umbraco.Automate.OpenIddict` automatically if it isn't already installed. No code changes are required — the connection type, actions, and triggers register themselves via attributes when the site starts.

## Step 3: Configure the Connected App credentials

Add the Client ID and Client Secret from Step 1 to your configuration (`appsettings.json`, `appsettings.Production.json`, environment variables, or a secret store — never commit the secret to source control):

```json
{
  "Umbraco": {
    "Automate": {
      "Providers": {
        "Salesforce": {
          "ClientId": "your-consumer-key",
          "ClientSecret": "your-consumer-secret"
        }
      },
      "Salesforce": {
        "ApiVersion": "v61.0"
      }
    }
  }
}
```

`ApiVersion` is optional — it defaults to a recent stable Salesforce REST API version. Only change it if you have a specific reason to pin an older or newer one.

**Connecting to a sandbox org?** The same `ClientId`/`ClientSecret` section is shared by both the production and sandbox connection types — Salesforce sandboxes typically use their own Connected App anyway (sandboxes don't inherit production Connected Apps automatically), so if you need both, register a second Connected App in the sandbox and point the `Salesforce` connection type at production, the `Salesforce (Sandbox)` connection type at the sandbox — see the note on multiple connections below.

## Step 4: Restart and connect

1. Restart the site. On first boot, the package's database migrations run automatically — no manual SQL required. You'll see log lines like `Running N pending Automate migrations` / `Automate migrations completed successfully`.
2. In the backoffice, go to **Automation → Connections → Create**.
3. Choose **Salesforce** (production) or **Salesforce (Sandbox)**.
4. Click **Authenticate with Salesforce** — you'll be redirected to Salesforce's own login/consent screen. Log in as a Salesforce user with API access and authorize the app.
5. Back in the backoffice, click **Test connection** — you should see a success message naming your Salesforce organization ID and the connected username.

That's the entire setup. You can now build automations using the Salesforce triggers and actions — see [docs/triggers.md](triggers.md) and [docs/actions.md](actions.md).

## Multiple connections / multiple orgs

You can create more than one Salesforce connection (e.g. a production org and a sandbox, or connections for multiple customer orgs in a multi-tenant setup) — each is authenticated independently and scoped to workspaces the same way other Automate connections are. Every action's connection picker lets you choose which one a given step uses.

## Adding a scope later

If a future action or your own automation needs a Salesforce OAuth scope you haven't granted yet:

1. Add the scope to the Connected App in Salesforce.
2. Add it to the `Scopes`/OAuth configuration if you've customized it beyond the defaults above.
3. Restart the site.
4. Re-authenticate the existing connection (**Automation → Connections → your connection → Disconnect**, then **Authenticate with Salesforce** again) — a scope change requires a fresh authorization, not just a restart.

## A hosting gotcha that looks like a bug but isn't

If you self-host on a single instance and a manually-triggered automation sits stuck in "Running" forever with no step ever executing, this is almost always **not** a problem with this package — it's Umbraco Automate's outbox dispatcher never becoming eligible to run because the site's server role can't be determined. Set these two values in your hosting configuration:

```json
{
  "Umbraco": {
    "CMS": {
      "WebRouting": { "UmbracoApplicationUrl": "https://your-real-site-url/" },
      "Global": { "DisableElectionForSingleServer": true }
    }
  }
}
```

See [docs/troubleshooting.md](troubleshooting.md) for this and other issues.
