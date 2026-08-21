# Umbraco Automate Salesforce

Salesforce connection, triggers, and actions for [Umbraco Automate](https://github.com/umbraco/Umbraco.Automate).

Mirrors the structure and conventions of `Umbraco.Automate.Slack` and
`Umbraco.Automate.OpenIddict`.

## Prerequisites

0. Umbraco CMS 17.x (the active LTS line — see `v17/dev` in the `umbraco/Umbraco.Automate` monorepo) with **Umbraco.Automate** (core) installed and composed.
   This package does nothing without it — see [docs/installation.md](docs/installation.md#step-0-prerequisites).
1. A Salesforce Connected App (or External Client App) with OAuth enabled.

`Umbraco.Automate.OpenIddict` is pulled in automatically as a transitive NuGet
dependency — you never install it by hand.

## Documentation

- [docs/installation.md](docs/installation.md) — Connected App setup, configuration, first connect.
- [docs/triggers.md](docs/triggers.md) — the one Salesforce trigger this package ships and why.
- [docs/actions.md](docs/actions.md) — the seven Salesforce actions, their inputs/outputs, and example use.
- [docs/security.md](docs/security.md) — the security/compliance posture this package targets.
- [docs/troubleshooting.md](docs/troubleshooting.md) — common Salesforce error codes and what to do about each.

## Verifying a packed release actually installs

Every build in this repo up to `scripts/pack-release.ps1` builds via a `ProjectReference` to a
sibling `../Umbraco.Automate` checkout (see below) — that proves the code works, not that the
*package* does. Before publishing a release:

```powershell
./scripts/pack-release.ps1                    # packs all 4 projects with real dependency pins
./scripts/install-package-test-site.ps1       # spins up a fresh site and installs from the pack output
```

The second script creates a genuinely separate Umbraco 17 site under `demos/v17/` and installs
`Umbraco.Automate` (from nuget.org) and `Umbraco.Automate.Salesforce` (from the local pack output)
as real NuGet packages — no project references. Confirm the server log shows `Running N pending
Automate migrations` / `Automate migrations completed successfully`, and that **Automation →
Connections → Create** lists both Salesforce connection types, before publishing.

## Repository home

This repo currently stands alone at `Salesforce v1/`. For local development it
expects a sibling checkout of the *whole* `umbraco/Umbraco.Automate` monorepo
at `../Umbraco.Automate`, checked out to **`v17/dev`** — the active LTS line
this package targets (the repo's default branch is `v18/dev`; don't build
against that one) — with **full history, not a shallow clone**: Core and
OpenIddict both use Nerdbank.GitVersioning, which needs full history to compute
a version and fails outright on `--depth 1`. Clone, switch branches, and (if
you did shallow-clone) unshallow before building:

```bash
git clone https://github.com/umbraco/Umbraco.Automate.git ../Umbraco.Automate
cd ../Umbraco.Automate && git checkout v17/dev
# If you cloned with --depth 1 at any point:
git fetch --unshallow origin v17/dev
```

That clone is the *monorepo root*, not the Core product folder directly — Core
and OpenIddict each live one level further in, under their own like-named
product folders within it (e.g. `../Umbraco.Automate/Umbraco.Automate/src/...`,
not `../Umbraco.Automate/src/...`). This package's `.csproj` files already
account for that extra nesting via their `ProjectReference` paths.

Without that sibling checkout, the build still works — it falls back to the
`Umbraco.Automate.Core` / `Umbraco.Automate.OpenIddict` NuGet packages pinned in
`Directory.Packages.props`.

## Project layout

```
Umbraco.Automate.Salesforce/
├── src/
│   ├── Umbraco.Automate.Salesforce/                    # Meta-package (bundles the three below)
│   ├── Umbraco.Automate.Salesforce.Core/                # Actions, Triggers, Connection, Composer
│   ├── Umbraco.Automate.Salesforce.Persistence.SqlServer/  # EF Core migrations (SQL Server)
│   └── Umbraco.Automate.Salesforce.Persistence.Sqlite/     # EF Core migrations (SQLite)
├── tests/
│   ├── Umbraco.Automate.Salesforce.Tests.Unit/
│   └── Umbraco.Automate.Salesforce.Tests.Integration/
└── Umbraco.Automate.Salesforce.slnx
```

## Build

```bash
dotnet build Umbraco.Automate.Salesforce.slnx
dotnet test Umbraco.Automate.Salesforce.slnx
```
