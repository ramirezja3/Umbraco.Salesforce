# Umbraco.Automate.Salesforce

Salesforce connection, triggers, and actions for [Umbraco Automate](https://github.com/umbraco/Umbraco.Automate).

Mirrors the structure and conventions of `Umbraco.Automate.Slack` and
`Umbraco.Automate.OpenIddict` — see [CLAUDE.md](CLAUDE.md) for the full brief and
the reasoning behind every structural choice below.

## Prerequisites

0. Umbraco CMS 17.x (the active LTS line — see `v17/dev` in the `umbraco/Umbraco.Automate` monorepo) with **Umbraco.Automate** (core) installed and composed.
   This package does nothing without it — see [docs/installation.md](docs/installation.md#step-0-prerequisites).
1. A Salesforce Connected App (or External Client App) with OAuth enabled.

`Umbraco.Automate.OpenIddict` is pulled in automatically as a transitive NuGet
dependency — you never install it by hand.

## Repository home

This repo currently stands alone at `Salesforce v1/`. For local development it
expects a sibling checkout of the `umbraco/Umbraco.Automate` monorepo at
`../Umbraco.Automate` (same relative depth Slack/OpenIddict use inside that
monorepo), checked out to **`v17/dev`** — the active LTS line this package
targets (the repo's default branch is `v18/dev`; don't build against that one).
So `UseProjectReferences=true` builds resolve Core/OpenIddict by project
reference instead of NuGet. Clone and switch branches before building:

```bash
git clone https://github.com/umbraco/Umbraco.Automate.git ../Umbraco.Automate
cd ../Umbraco.Automate && git checkout v17/dev
```

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
