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

### Verifying a packed release actually installs

Every build in this repo up to `pack-release.ps1` builds via a `ProjectReference` to a sibling
`../Umbraco.Automate` checkout (see "Local development setup" below) — that proves the code works,
not that the *package* does. Before publishing a release:

```powershell
./scripts/pack-release.ps1                    # packs the package with real dependency pins
./scripts/install-package-test-site.ps1       # spins up a fresh site and installs from the pack output
```

The second script creates a genuinely separate Umbraco 17 site under `demos/v17/` and installs
`Umbraco.Automate` (from nuget.org) and `Umbraco.Automate.Salesforce` (from the local pack output)
as real NuGet packages — no project references. Confirm that **Automation → Connections → Create**
lists the Salesforce connection type, and that the action picker shows all six Salesforce
actions, before publishing.

### Local development setup

This repo stands alone — unlike `Umbraco.Automate.Slack`, it isn't part of the `umbraco/Umbraco.Automate`
monorepo, so there's no shared root `CLAUDE.md`/build templates to inherit. For local development it
expects a sibling checkout of the *whole* `umbraco/Umbraco.Automate` monorepo at `../Umbraco.Automate`,
checked out to **`v17/dev`** — the active LTS line this package targets (the repo's default branch is
`v18/dev`; don't build against that one) — with **full history, not a shallow clone**: Core and
OpenIddict both use Nerdbank.GitVersioning, which needs full history to compute a version and fails
outright on `--depth 1`.

```bash
git clone https://github.com/umbraco/Umbraco.Automate.git ../Umbraco.Automate
cd ../Umbraco.Automate && git checkout v17/dev
# If you cloned with --depth 1 at any point:
git fetch --unshallow origin v17/dev
```

That clone is the *monorepo root*, not the Core product folder directly — Core and OpenIddict each
live one level further in, under their own like-named product folders within it (e.g.
`../Umbraco.Automate/Umbraco.Automate/src/...`, not `../Umbraco.Automate/src/...`). This package's
`.csproj` files already account for that extra nesting via their `ProjectReference` paths.

Without that sibling checkout, the build still works — it falls back to the
`Umbraco.Automate.Core` / `Umbraco.Automate.OpenIddict` NuGet packages pinned in
`Directory.Packages.props`.

## Architecture Overview

Umbraco.Automate.Salesforce is a provider package that adds Salesforce connectivity to Umbraco Automate. It uses `Umbraco.Automate.OpenIddict` for OAuth authentication and provides a Salesforce connection type and six actions — no triggers (removed in v2; see `docs/dev-notes.md`). Each action targets one fixed, named Salesforce object with named fields (no generic "pick an object API name" action — see `docs/dev-notes.md`'s most recent entry for why). Every action calls the Salesforce REST API directly and keeps no local state, so this package needs no persistence of its own — it's a single Razor SDK RCL, the same shape as `Umbraco.Automate.Slack`, not split the way `Umbraco.Automate.OpenIddict` is.

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

1. `SalesforceComposer` registers one OpenIddict Client WebIntegration registration — `Salesforce` (`login.salesforce.com`) — plus this package's own services. No sandbox (`test.salesforce.com`) connection type is shipped.
2. `SalesforceConnectionType` defines the connection type using `OAuthConnectionTypeBase`.
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
        }
      },
      "Salesforce": {
        "ApiVersion": "v62.0"
      }
    }
  }
}
```

`Umbraco:Automate:Providers:Salesforce` is the OAuth app credentials (bound generically by `Umbraco.Automate.OpenIddict`, including its `Scopes` array — see `SalesforceComposer.ResolveScopes`); `Umbraco:Automate:Salesforce` is this package's own REST API behavior (API version, query row cap, retry/backoff — see `SalesforceApiOptions`).

### Project Layout

```
Umbraco.Automate.Salesforce/
├── src/
│   └── Umbraco.Automate.Salesforce/    # Actions, Connection, Configuration — the one NuGet package
├── tests/
│   ├── Umbraco.Automate.Salesforce.Tests.Unit/
│   └── Umbraco.Automate.Salesforce.Tests.Integration/
└── Umbraco.Automate.Salesforce.slnx
```

No persistence project and no meta-package split (unlike `Umbraco.Automate.OpenIddict`'s
multi-package shape) — this package ships no triggers and keeps no local state, so it's a single
Razor SDK class library, the same shape as `Umbraco.Automate.Slack`.

## Dependencies

- Umbraco.Automate.Core
- Umbraco.Automate.OpenIddict
- OpenIddict.Client.WebIntegration 7.4.x

## Commit Scopes

Use these scopes for conventional commits affecting this package:

`salesforce`
