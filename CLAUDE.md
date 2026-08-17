# CLAUDE.md — Umbraco.Automate.Salesforce

This file is the working brief for an AI coding agent (or a human) building **Umbraco.Automate.Salesforce**: a NuGet package that adds Salesforce as a first-class **provider** to [Umbraco Automate](https://github.com/umbraco/Umbraco.Automate), Umbraco's open-source, event-driven automation engine for Umbraco CMS 17+.

It should be **plug-and-play**: an implementer installs the NuGet package(s), drops in Salesforce connected-app credentials, and gets working triggers/actions in the backoffice automation canvas — no custom code, no manual database work, no guesswork.

> **Golden rule: mirror `Umbraco.Automate.Slack`.** That package is the reference implementation for "how a provider is built" in this ecosystem. Whenever you are unsure how something should be structured, named, registered, tested, or documented — go read the equivalent piece in `Umbraco.Automate.Slack` (and its `CLAUDE.md`) and follow the same pattern. Do not invent a parallel architecture. Consistency with the Slack package is a correctness requirement, not a style preference.

---

## 0. Prerequisite: Umbraco Automate must already be installed

**This package is an add-on, not a standalone product.** It does nothing on its own. It only works on a site that already has:

1. **Umbraco CMS 17.x** running.
2. **`Umbraco.Automate`** (the core automation engine NuGet package) installed and composed — this is what provides the visual canvas, the workflow/run engine, the Connections/Workspaces model, and the extensibility points (`IAutomateProvider`/trigger & action registration, etc.) that this package plugs into.
3. **`Umbraco.Automate.OpenIddict`** installed — this package's Salesforce connection type is built on top of it (see §2/§4). Umbraco's own Slack add-on installs this automatically as a transitive dependency; **`Umbraco.Automate.Salesforce` must do the same** — the implementer should never need to manually `dotnet add package Umbraco.Automate.OpenIddict` themselves.

Concretely, this means:

- The `.csproj`/`.nuspec` for `Umbraco.Automate.Salesforce` must declare a NuGet **package dependency** on `Umbraco.Automate` (core) and `Umbraco.Automate.OpenIddict`, pinned to compatible version ranges via the solution's `Directory.Packages.props`, exactly the way `Umbraco.Automate.Slack` declares its dependency on Core + OpenIddict. Installing this package via NuGet pulls them in automatically if missing — this is enforced by the package manager, not by application code.
- **There is no explicit "fail fast if Core is missing" runtime check to write, and don't add one.** Confirmed against the real Slack and OpenIddict source: no such check exists anywhere in the monorepo, because it's structurally unnecessary — Core is a hard compile-time `PackageReference`/`ProjectReference`, so the assembly cannot load at all without it. Don't invent defensive composition-guard code that has no precedent in this codebase; it adds a pattern the rest of the ecosystem doesn't use.
- Registration itself needs no interface implementation or manual wiring at all (see corrected §1a below) — there is no `IAutomateProvider` interface. Triggers, actions, and connection types are plain classes decorated with attributes (`[Action(...)]`, `[Trigger(...)]`, `[ConnectionType(...)]`) that Umbraco's `TypeLoader` auto-discovers via `GetTypesWithAttribute<...>` at startup, wired once inside Core's own composer. A provider package's only job is to make sure those attributed classes exist in its assembly and get loaded — it does not register them itself.
- Documentation (§10) must state this prerequisite as **step 0**, before Salesforce connected-app setup, with a link to Umbraco Automate's own installation docs.

Add "Umbraco Automate core installed (as a NuGet/project dependency, confirmed buildable)" as the top line item in the Definition of Done checklist in §12. For local development, reuse the monorepo's `install-demo-site` script pattern (Core + add-ons wired into one demo site) rather than standing up Salesforce against Core in isolation.

### 0a. Correction log — keep this section updated

This brief was originally written from documentation and a directory listing, before anyone had read the actual Slack/OpenIddict source line by line. Once an agent (human or AI) inspects the real code, **update this file** rather than silently deviating from it — that's the whole point of a corrections log: the next session shouldn't have to rediscover the same facts. Confirmed corrections so far:

| Original assumption in this brief | What the real source actually shows |
|---|---|
| Provider packages implement an `IAutomateProvider` interface and register triggers/actions/connection types themselves in an `IComposer` | No such interface exists. Registration is pure attribute-based auto-discovery (`[Action]`, `[Trigger]`, `[ConnectionType]` + `TypeLoader.GetTypesWithAttribute<...>`), performed once by Core. A provider drops attributed classes into its assembly; it does not call any registration API itself. |
| A composer should fail fast with a clear error if Core isn't present | Not needed and has no precedent — Core is a hard package/project dependency, so the assembly literally cannot load without it. Don't add this code. |
| Slack follows a Core/Persistence/Web/Startup project split, like the root Umbraco.Automate package | That split is specific to the *core* `Umbraco.Automate` package. `Umbraco.Automate.Slack` is a **single Razor class library (RCL) project** — no split, no separate persistence project, no separate UI project, no test project. It has exactly one connection type and one action (`SendMessageAction`), zero triggers, and reuses a **generic OAuth connection picker** already provided by `Umbraco.Automate.OpenIddict` rather than building custom UI. |
| There's a live-metadata-picker precedent to follow for object/field dropdowns | There isn't one anywhere in the codebase. Slack's own channel field is a **plain text input**, not a live picker. Building a live Salesforce object/field picker (§7) is new ground for this ecosystem — treat it as a v2 follow-up requiring its own Management API endpoint + a dedicated client/UI project, not a v1 requirement. Ship v1 actions/triggers with plain text object/field inputs. |
| The monorepo's default branch tracks the CMS version this brief targets ("17+") | It doesn't — the default branch is `v18/dev` (`Umbraco.Cms.Core` [18.0.0,…), `Umbraco.Automate.Core` [18.2.0,…)). `v17/dev` is a **separate, actively-supported LTS line** ("Features + bug fixes" per CONTRIBUTING.md, not EOL) with its own, older pins (`Umbraco.Cms.Core` [17.4.0,…), `Umbraco.Automate.Core` [17.2.0,…), `OpenIddict.Client.WebIntegration` [7.4.0,…)). `Directory.Packages.props` in this repo is pinned to `v17/dev`'s actual values — verify against that branch specifically, not whatever the repo's default branch happens to show, before bumping anything. |
| A single Salesforce connection type with a configurable login-host field can support Production + Sandbox + multi-org (§2 non-negotiables #5/#6) | It can't, and there's no way to make it work without changing `Umbraco.Automate.OpenIddict` (not allowed). Confirmed by reading `OAuthChallengeController`/`OpenIddictClientCredentialsConfigurator`: every OpenIddict Client registration has **one fixed issuer, set once at startup from one appsettings section**, keyed by provider name — a connection's settings can't parameterize which registration a challenge targets (the generic `Umb.Automate.OAuth` picker's `provider` value is a literal in `EditorConfig`, not a runtime field read). **Fix implemented:** two separate connection types/registrations — `SalesforceConnectionType` (alias `salesforce`, provider `Salesforce`, issuer `https://login.salesforce.com/`) and `SalesforceSandboxConnectionType` (alias `salesforce-sandbox`, provider `SalesforceSandbox`, issuer `https://test.salesforce.com/`) — verified by instantiating `OpenIddictClientOptions` with both registrations side by side; OpenIddict accepts two registrations sharing one `ProviderType` as long as `ProviderName`/`RegistrationId` differ. This still doesn't cover org-enforced custom "My Domain"-only login (no generic `login.salesforce.com` fallback) — out of scope for v1, would need a Slack-style dynamic endpoint-override event handler; flagged, not built. |
| Salesforce's org-specific API base (`instance_url`) needs a new persistence table to survive between the OAuth callback and later action calls | It doesn't. `OAuthCredentials.AccountLabel` (an existing, unencrypted, 500-char display column Slack already repurposes for "team name") is generic storage with no Core/OpenIddict change required to reuse. `SalesforceOAuthHandlers.ExtractInstanceUrl` reads the non-standard `instance_url` field Salesforce adds to the standard OAuth token response (present for **both** registrations, at zero extra API calls) and stashes it there via the same `context.Properties[...AccountLabel]` mechanism Slack's own handlers use; `SalesforceConnectionResolver` reads it back out. **Net effect: v1 needs no database migration at all** — contradicts this brief's original §2/§5 conclusion below that a persistence project was required. The already-scaffolded `Persistence.SqlServer`/`Persistence.Sqlite` projects are kept anyway (harmless, empty) because the deferred v2 Describe-metadata cache genuinely will need them — just not yet. |
| An action's declared `[Action(..., ConnectionTypeAlias = "...")]` can name more than one acceptable connection type, or actions can otherwise work against either connection type interchangeably | Confirmed false by reading the canvas frontend (`node-settings-modal.element.ts`): the per-step connection picker filters with a strict `item.type === connectionTypeAlias` equality check, and the attribute property is a single nullable `string`, not a list. **v1 decision:** all actions declare `ConnectionTypeAlias = "salesforce"` (production) only. The sandbox connection type still exists (so implementers can authenticate a sandbox and use its `ValidateAsync` check), but no actions are wired to it yet — using one against a sandbox org requires the platform to gain either a multi-type/array `ConnectionTypeAlias` or duplicating every action per environment, neither of which is done in this pass. Worth raising with the Umbraco Automate core team as a platform gap rather than working around cheaply. |
| A raw, bindable SOQL template field can be made injection-safe by escaping bound values inside the action | It can't, fully. Core's `${ }` binding substitution runs **before** `ExecuteAsync` — by the time an action sees the settings string, any bound value is already spliced in as plain text, with no hook for the action to intervene per-value. `QueryRecordsAction` therefore only defends what's checkable post-substitution (must start with `SELECT`, no semicolon, forced/capped `LIMIT`) and ships `SalesforceSoqlEscaper.EscapeStringLiteral` as an opt-in helper for automation authors who build a `WHERE ... = '${binding}'` clause themselves — it cannot make a single free-text bindable SOQL field safe by itself. This is documented in `QueryRecordsAction`'s XML doc rather than silently claimed as solved. |

**Because Salesforce genuinely needs its own persistence** (the Describe-metadata cache table in §5 — something Slack has no equivalent of), **model this package's project structure on `Umbraco.Automate.OpenIddict`'s split, not Slack's single-project shape**: OpenIddict ships as Core + `Persistence.SqlServer` + `Persistence.Sqlite` + a bundling meta-package that references both. That's the closest real precedent in this codebase for "a provider that needs its own tables," and §2/§9 below have been updated accordingly. (See the `instance_url` row above, though: that specific need turned out not to require the persistence layer after all — the project split is kept for the still-real, still-deferred Describe-cache need.)

**Status as of this pass:** connection types (production + sandbox), the OAuth composer/handlers, the shared Salesforce REST client (error mapping, rate-limit backoff with `Retry-After`/jitter), and six actions — Create/Update/Upsert/Get/Delete/Query Records — are implemented and building/testing green against the real `v17/dev`-pinned packages (`dotnet build`/`dotnet test` on `Umbraco.Automate.Salesforce.slnx`). Convert Lead, Add Chatter Post, Send Email, Attach File, and Run Apex REST (§7) are not started. No triggers (§6) are started — per this file's own build-order note, they need their own design pass for the CDC/Pub-Sub listener infrastructure, which doesn't exist as a precedent anywhere in this codebase. No custom UI, no docs beyond this file, no integration test fixtures yet.

---

## 1. Reference material (read this first)

Before writing any code, pull down and actually read these — don't rely on memory of what they "probably" contain:

- `https://github.com/umbraco/Umbraco.Automate` — monorepo root. Read the root `CLAUDE.md` and `README.md`.
- `Umbraco.Automate/` — the core package (workflow engine, triggers/actions abstractions, connections, workspaces). Read `Umbraco.Automate/CLAUDE.md`.
- `Umbraco.Automate.OpenIddict/` — reusable OAuth client infrastructure (built on OpenIddict Client WebIntegration). Read `Umbraco.Automate.OpenIddict/CLAUDE.md`. **Salesforce OAuth (Web Server / Authorization Code + refresh token flow) should be implemented on top of this, exactly the way Slack's OAuth is**, not with a bespoke OAuth client.
- `Umbraco.Automate.Slack/` — **the template for the parts of this package that have a direct Slack equivalent** (a single connection type, actions, config-driven OAuth scopes). It is *not* a template for persistence, custom UI, or triggers — Slack has none of those. Read `Umbraco.Automate.Slack/CLAUDE.md` line by line and confirm, rather than assume:
  - it is one RCL project (no Core/Persistence/Web split) — see §0a
  - namespace and folder conventions actually used inside that single project (confirm exact folder names against the real repo, don't guess)
  - registration is attribute-based auto-discovery, not an `IComposer`-driven manual registration call — see §0a
  - how `SendMessageAction` defines its input schema for the visual canvas, since this is the direct pattern for every Create/Update/Upsert/Query/etc. action in §7
  - how the Slack connection type reuses `Umbraco.Automate.OpenIddict`'s generic OAuth connection picker UI rather than shipping bespoke UI — reuse the same generic picker for Salesforce's connection screen unless something Salesforce-specific (e.g. sandbox vs. production host selection) genuinely requires a custom field on top of it
  - how OAuth scopes are configured in `appsettings.json`
  - how errors/retries surface in the automation Run log
  - changelog, versioning, and conventional-commit conventions
- `Umbraco.Automate.OpenIddict/` (again, specifically for structure this time) — **the template for this package's project layout**, because it's the closest real precedent for "a provider that ships its own persistence": Core + `Persistence.SqlServer` + `Persistence.Sqlite` + a bundling meta-package. Mirror that split for `Umbraco.Automate.Salesforce` rather than Slack's single-project shape (see §0a and §2).
- `docs/engineering-spec.md` and `docs/identity-ownership-permissions.md` in the monorepo — the platform's contracts for how a provider must behave to be considered "well-behaved" (permission scoping to workspaces, connection ownership, audit logging, etc.)
- Umbraco's public docs: `https://docs.umbraco.com/umbraco-automate/add-ons/slack/installation` and the sibling pages — this is the *installation experience* an implementer has for Slack today. The Salesforce install experience must read the same way: register an app, add a redirect URL, paste a client ID/secret into config, restart, authenticate. No steps beyond that should be needed.

If anything below conflicts with what you find in the real Slack/OpenIddict source, **the real source wins** — update §0a with the correction and proceed on the corrected basis. This document describes intent; the actual repos are ground truth for mechanics.

---

## 2. What this package is

**Umbraco.Automate.Salesforce** — a Salesforce connection provider for Umbraco Automate, dependent on Core + OpenIddict but **structured like `Umbraco.Automate.OpenIddict` (Core/Persistence split), not like the single-project `Umbraco.Automate.Slack`** — because this package, unlike Slack, needs its own database table (the Describe-metadata cache in §5). See §0a for why.

```
Umbraco.Automate (Core)                       ← NuGet/project dependency, not modified
    └── Umbraco.Automate.OpenIddict           ← NuGet/project dependency, not modified
        └── Umbraco.Automate.Salesforce       ← THIS PACKAGE (Provider), split like OpenIddict:
              ├── Umbraco.Automate.Salesforce                    (core: connection type, actions, triggers, attributed classes)
              ├── Umbraco.Automate.Salesforce.Persistence.SqlServer
              ├── Umbraco.Automate.Salesforce.Persistence.Sqlite
              └── Umbraco.Automate.Salesforce (meta-package)     ← what implementers actually install; references the above
```

Confirm the exact project names against the real `Umbraco.Automate.OpenIddict` source before finalizing — the table above is the pattern, not a verified literal naming scheme. Ship this as the single NuGet package an implementer installs (the bundling meta-package), so "plug and play" still means one `dotnet add package` / one Marketplace install, even though multiple assemblies ship under the hood.

**No custom backoffice UI project in v1.** Reuse `Umbraco.Automate.OpenIddict`'s generic OAuth connection picker for the Salesforce connection screen (same as Slack does), and ship v1 action/trigger config with plain text object/field inputs (no live Describe-metadata picker — see §0a and §7). A dedicated UI/Management-API project for a live picker is an explicit, separately scoped v2 item, not part of this build.

### Non-negotiables

1. **Zero required code from the implementer.** Install the package, add Salesforce connected-app Client ID/Secret (+ optionally My Domain URL) to configuration, restart, click "Connect" in the backoffice, authorize. That's the entire setup.
2. **Auto-discovered composition.** Registration happens via an `IComposer`/manifest exactly like Slack's, with no manual `Program.cs` wiring.
3. **Database migrations run automatically** on first boot after install, using Umbraco's `PackageMigrationPlan` / `MigrationPlan` mechanism (same as Core/Slack) — the implementer never runs SQL by hand. See §5.
4. **OAuth via `Umbraco.Automate.OpenIddict`.** Do not hand-roll token storage, refresh logic, or the callback endpoint — extend the existing OpenIddict-based connection type the way Slack does, registering Salesforce as a provider profile (authorization endpoint, token endpoint, revocation endpoint, PKCE settings, default scopes).
5. **Multi-org support.** A single Umbraco instance must be able to hold more than one Salesforce **Connection** (e.g. Sandbox + Production, or multiple customer orgs in a multi-tenant setup), scoped to workspaces exactly like Slack connections are.
6. **Sandbox vs. Production awareness.** Salesforce has distinct login hosts (`login.salesforce.com` vs `test.salesforce.com`, plus custom "My Domain" hosts). The connection setup UI must let the implementer pick or supply the correct authorization host, and store it per-Connection.
7. **Respect Salesforce API limits.** Every outbound action must be resilient to Salesforce's daily API call limits and per-request governor limits: exponential backoff on `REQUEST_LIMIT_EXCEEDED`, honoring `Retry-After`, and surfacing a clear, actionable error in the Run log rather than a raw stack trace.
8. **Least-privilege OAuth scopes.** Only request the scopes actually needed by enabled actions (mirrors Slack's scope-per-feature model: docs must show which Salesforce OAuth scope unlocks which action, e.g. `api`, `refresh_token`, `offline_access`).
9. **No secrets in source control, logs, or the automation Run history.** Client secret lives in configuration (`appsettings.json` / environment variables / key vault) only. Access/refresh tokens are encrypted at rest using the same protection mechanism Core/Slack already uses for connection secrets (ASP.NET Data Protection or equivalent — confirm against Slack's implementation, do not roll new crypto).
10. **Idempotency on writes.** Actions that create/update Salesforce records must support an idempotency key or upsert-by-external-ID pattern where Salesforce supports it (`Upsert` REST calls against an External ID field), so a retried automation step doesn't create duplicate records.

---

## 3. Target framework & stack

Match the monorepo exactly (verify current values in `global.json` / `Directory.Packages.props` / root `README.md` before starting — these move over time):

- **.NET 10.0** (`net10.0`)
- **Umbraco CMS 17.x** (backoffice extension APIs, Umbraco.Automate 17-compatible)
- **WorkflowCore** (same version pinned by the monorepo) — triggers/actions are WorkflowCore steps under the hood, same as Slack's
- **Umbraco.Automate.OpenIddict** for OAuth (which itself wraps **OpenIddict Client WebIntegration**). Salesforce's OAuth is **standard OIDC** (unlike Slack's non-standard OAuth v2 flow, which needed custom event handlers in OpenIddict's pipeline) — check whether `UseWebProviders()` already has a built-in Salesforce entry before writing any Slack-style custom handlers. There is a real chance Salesforce needs *less* custom OAuth code than Slack did, not more; verify before assuming otherwise.
- **Central Package Management** via a `Directory.Packages.props` at the solution root — no floating versions in `.csproj` files
- **No custom backoffice UI project in v1** (§0a, §2) — reuse OpenIddict's generic OAuth connection picker. If/when a v2 live metadata picker is built, that's the point at which a Lit/TypeScript client project becomes necessary; OpenIddict.Core has a precedent for a client project backing OAuth screens, Slack does not have one at all.
- Salesforce API access via the **Salesforce REST API** (and **Bulk API 2.0** only if/when a bulk-oriented action genuinely needs it — do not default to Bulk API for single-record actions). Do not use the SOAP API. Use `/services/data/vXX.X/` with the API version configurable, defaulting to whatever is currently the latest stable Salesforce REST API version at build time.

---

## 4. Salesforce connected app setup (the implementer's side)

Document this exactly as thoroughly as Slack's installation doc, because it's the only manual step the user performs:

1. In Salesforce Setup → App Manager → **New Connected App** (or External Client App, per Salesforce's current guidance — check current Salesforce docs, this has shifted over time).
2. Enable OAuth Settings.
3. Callback URL: `https://<your-site>/umbraco/automate/oauth/callback/salesforce`.
4. OAuth scopes to request at minimum: `api` (access and manage data), `refresh_token`/`offline_access` (maintain a long-lived connection). Document additional scopes required by optional actions (e.g. `chatter_api` if a Chatter post action ships).
5. Enable **"Require Proof Key for Code Exchange (PKCE)"** if the OpenIddict integration supports it (Slack's likely does; match it).
6. Note Consumer Key (Client ID) and Consumer Secret (Client Secret).
7. Add to `appsettings.json` (mirror Slack's config shape 1:1, e.g. under `Automate:Salesforce` or whatever section Slack uses, adapted):
   ```json
   {
     "Automate": {
       "Salesforce": {
         "ClientId": "...",
         "ClientSecret": "...",
         "LoginHost": "login.salesforce.com",
         "Scopes": [ "api", "refresh_token" ]
       }
     }
   }
   ```
8. Restart the site, go to the Automate connections screen in the backoffice, click **Connect**, authenticate as a Salesforce user with API access, authorize.
9. If a new action later requires an additional scope: add it to the Connected App, add it to `Scopes` in config, restart, **re-authenticate the existing connection** (same re-auth flow Slack documents).

Write this up as `docs/installation.md` in the same voice/format as the Slack installation doc linked above.

---

## 5. Data & migrations

- Do **not** hand-write SQL for the implementer to run. Slack itself needs no migrations (no persistence of its own — it reuses Core's `Connections` table). **This package is the first Salesforce-side thing in the ecosystem that actually needs a migration**, so model it on `Umbraco.Automate.OpenIddict`'s persistence pattern instead: a startup migration notification handler analogous to OpenIddict's `RunOpenIddictMigrationNotificationHandler`, backed by per-provider `Persistence.SqlServer` / `Persistence.Sqlite` projects (§2), versioned so re-running is a no-op.
- Reuse Core's existing `Connections` table/entity for storing the Salesforce connection record itself (org ID, instance URL, login host, encrypted tokens, scopes granted, connected-by user, workspace scope) — this is exactly what the Connection-type extension point is for; don't duplicate it. Only add a new table for the genuinely Salesforce-specific concern:
  - a small lookup/cache table for the org's object/field Describe metadata, feeding the **v2** live picker (§0a, §7). **Not required for v1**, since v1 ships plain text object/field inputs — build the table now if convenient, but don't block v1 shipping on the picker UI that consumes it.
- Any cache table must degrade gracefully (empty/stale cache blocks nothing; a v2 field/object picker would fall back to plain text until refreshed).
- Confirm the actual table-naming convention against OpenIddict's real persistence project (not Slack's, which has none) before naming anything.

---

## 6. Triggers to build

**Build these after the actions in §7, not before — see the note at the end of this section.** Model these as WorkflowCore-backed Automate triggers, same shape as Core's built-ins (Webhook, Content Published, etc.), each with a typed output object usable via `${ }` bindings downstream, and registered the same attribute-based way described in §0a (a `[Trigger(...)]`-decorated class — no manual registration call).

| Trigger | Fires on | Key outputs |
|---|---|---|
| **Record Created** | A configured Salesforce object (Lead, Contact, Account, Opportunity, Case, or any custom object) has a new record created | Record Id, object type, all field values (typed per the object's describe metadata), created-by user, created timestamp |
| **Record Updated** | A configured object's record is updated, optionally filtered to specific fields changing | Record Id, changed fields (old/new values), full current record snapshot, updated-by user |
| **Record Deleted** | A configured object's record is deleted | Record Id, object type, deleted timestamp |
| **Opportunity Stage Changed** | An Opportunity's `StageName` changes, optionally filtered to a specific target stage (e.g. "Closed Won") | Opportunity Id, previous stage, new stage, Amount, Account Id, Owner |
| **Case Created / Case Status Changed** | A support Case is created or its Status changes | Case Id, Subject, Status, Priority, Contact Id, Owner |
| **Lead Converted** | A Lead is converted | Lead Id, resulting Contact/Account/Opportunity Ids |
| **Platform Event Received** | A Salesforce Platform Event of a configured type is published | Full event payload (typed against the event's schema) |
| **Webhook / Outbound Message Received** | Salesforce Outbound Message or a configured Apex-triggered callout hits Automate's inbound webhook endpoint | Raw payload, parsed fields |

Implementation notes:
- Prefer the **Change Data Capture (CDC)** event stream or the **Streaming API / Platform Events (CometD/Pub/Sub API)** for near-real-time Create/Update/Delete triggers over polling. Polling is an acceptable fallback (configurable interval, respecting API limits) but CDC/Pub-Sub should be the default where the org supports it — document the tradeoff clearly, and detect at connection time whether CDC is enabled on the org and warn in the UI if not.
- **v1 object/field selection is a plain text input, not a live picker.** There is no precedent anywhere in this codebase for a live-metadata dropdown (confirmed in §0a) — Slack's equivalent field is plain text too. Wire it up to the Describe-metadata cache (§5) as a v2 enhancement once a Management API endpoint + UI project exist for it; don't block v1 on this.
- Every trigger must be filterable by object type at minimum; field-level filters where listed above.
- **Triggers have no existing external-system precedent in this codebase** — Core's built-in triggers are all CMS-notification-driven (Content Published, etc.), not backed by an external API's event stream. Implementing Record Created/Updated/Deleted and Platform Event triggers requires a **new hosted background service** (a CDC/Pub-Sub listener or poller) that calls into the platform's trigger dispatcher — the closest existing analogue to build from is Core's scheduled-trigger background job, but the CDC/Pub-Sub listener itself is new work, not a mirror of something that already exists. Scope it as its own design pass rather than assuming it falls out of the action-building pattern.
- **Build order: do the actions in §7 first, triggers second.** Actions map directly onto Slack's single existing action (`SendMessageAction`) pattern with no new plumbing required. Triggers need the new background-service work above, so they're a bigger, less-understood lift — validate the whole connection/action/migration pipeline end-to-end on actions before taking on trigger infrastructure.

---

## 7. Actions to build

| Action | What it does | Notes |
|---|---|---|
| **Create Record** | Creates a record of a configured Salesforce object type with mapped field values | Field mapping UI driven by live Describe metadata; supports formula/binding values from earlier steps |
| **Update Record** | Updates a record by Id with mapped field values | |
| **Upsert Record** | Create-or-update by an External ID field | The idempotent, retry-safe write path — prefer this in generated example automations over plain Create |
| **Get Record** | Retrieves a record by Id (or by SOQL/lookup) for use in later steps | |
| **Delete Record** | Deletes a record by Id | Should require an explicit confirmation flag in config to reduce accidental destructive automations |
| **Query Records (SOQL)** | Runs a bounded SOQL query and returns matching records | Enforce a max row count / pagination; block obviously unsafe/unbounded queries; parameterize inputs, never string-concatenate user/binding values into SOQL (SOQL injection prevention) |
| **Convert Lead** | Converts a Lead, optionally creating/associating an Opportunity | |
| **Add Chatter Post** | Posts a Chatter feed item to a record or user | Requires `chatter_api` scope |
| **Send Email (Salesforce)** | Sends an email via Salesforce's Email API against a template or ad hoc body, associated with a record | Distinguish clearly from Umbraco's own "Send Email" core action in docs, since both may exist in the same automation |
| **Attach File** | Uploads a file (e.g. from Umbraco media or a prior step's output) as a Salesforce ContentDocument linked to a record | |
| **Run Apex REST / Invocable Action** | Calls a custom Apex REST endpoint or invokes an Invocable Action/Flow exposed on the org | The escape hatch for anything not covered above; still goes through the SSRF/URL-allowlist protections Core's HTTP Request action uses |

Implementation notes:
- Every write action returns the resulting record Id(s) and a success/failure status usable downstream.
- Every action must map Salesforce API errors (`INVALID_FIELD`, `REQUIRED_FIELD_MISSING`, `DUPLICATE_VALUE`, `FIELD_CUSTOM_VALIDATION_EXCEPTION`, governor-limit errors, etc.) into a human-readable message in the Run log, not a raw JSON dump — same UX bar as Slack's action error handling.
- Batch-friendly where sensible (e.g. Create/Update/Upsert should accept either a single mapped record or a collection from a prior step, using Salesforce's Composite/Batch REST API under the hood, not N sequential calls) — this matters for staying under governor and API-call limits.

---

## 8. Security & compliance checklist ("Grade A", no cutting corners)

- [ ] Client secret and tokens never appear in logs, exception messages, or the Run history UI (redact/mask, same as Slack's connection secrets).
- [ ] Tokens encrypted at rest using the platform's existing Data Protection mechanism — reuse it, don't reinvent it.
- [ ] All Salesforce API calls over HTTPS only; TLS validation never disabled.
- [ ] SOQL/SOSL inputs are parameterized/escaped — no injection via bound values.
- [ ] Outbound webhook endpoint (Outbound Message / custom callout receiver) validates request signatures/shared secrets, same pattern as Core's signature-verified Webhook trigger — never trust an unauthenticated inbound payload.
- [ ] Rate limiting / backoff on Salesforce 429s and `REQUEST_LIMIT_EXCEEDED`, with jitter, and a circuit breaker so one runaway automation can't exhaust the org's daily API allocation.
- [ ] Connections are scoped to Workspaces and respect Core's existing permission model — a user without access to a workspace cannot see or use its Salesforce connection.
- [ ] Destructive actions (Delete Record) require explicit opt-in in config and are clearly labelled as destructive in the canvas UI.
- [ ] Package passes the same static analysis / analyzer ruleset as the rest of the monorepo (`.editorconfig`, existing Roslyn analyzers) with zero new warnings.
- [ ] Dependency versions pinned centrally; no direct Salesforce SDK dependency with known CVEs (check current advisories before pinning).
- [ ] GDPR-aware defaults given likely Danish/EU customers: document what personal data flows through triggers/actions (record fields may contain PII), don't cache field *values* beyond what's needed for the current run, and make the metadata cache (§5) hold only schema (object/field names/types), never row data.
- [ ] Threat-model note in `docs/security.md`: OAuth token theft, SSRF via the "Run Apex REST" action's target URL, SOQL injection, webhook spoofing, and over-broad scopes — one paragraph each, with the mitigation implemented.

---

## 9. Testing bar ("enterprise tested")

Mirror Slack's test project layout and coverage philosophy exactly:

- **Unit tests**: field-mapping logic, SOQL builder/parameterization, error-mapping, backoff/retry policy, scope-to-action gating — all pure logic, no live Salesforce dependency, run in CI on every PR. Confirmed: Slack itself has **no test project at all**, so there's no existing HTTP-mocking convention in this monorepo to inherit — this package is setting a new precedent, not following one.
- **Integration tests**: against a Salesforce sandbox/scratch org, or a recorded HTTP fixture layer. **A WireMock (or equivalent) dependency does not exist anywhere in the monorepo today** — introducing one for this package's `*.Tests.Integration` project is a genuinely new addition, and should be called out as such in review (new third-party test dependency, not an existing convention being reused). Cover: OAuth token refresh, Create/Update/Upsert/Delete round-trips, CDC/streaming trigger firing, governor-limit error handling, pagination on Query.
- **Migration tests**: package installs cleanly into a fresh Umbraco 17 site and an upgrade-from-previous-version site; migrations are idempotent (running twice is a no-op). This exercises new ground for a Salesforce-family package (§5) — validate against the OpenIddict persistence project's own migration tests as the closest precedent, since Slack has no migrations to test.
- **Backoffice UI tests**: not applicable to v1, since v1 ships no custom UI project (§0a, §2) — the connection screen is OpenIddict's existing generic picker, which is presumably already tested upstream. Add UI/e2e tests only once a v2 UI project exists.
- **Load/resilience test**: simulate Salesforce rate-limiting responses and confirm backoff behaves and the Run log reports clearly rather than the automation silently hanging or crashing the host.
- CI pipeline mirrors the monorepo's existing pipeline definition (`azure-pipelines.yml` / GitHub Actions) — same build/test/pack/publish stages, same NuGet feed target.
- No merge without green tests and zero new analyzer warnings, same bar as the rest of the monorepo.

---

## 10. Documentation to ship

In the same shape/tone as Slack's docs on `docs.umbraco.com`:

- `README.md` — what it is, quick start, links to install guide.
- `docs/installation.md` — connected app setup + config (§4), step by step, screenshots optional but structure identical to Slack's.
- `docs/triggers.md` / `docs/actions.md` — one entry per trigger/action from §6/§7: what it does, required scopes, inputs, outputs, example use.
- `docs/security.md` — the checklist in §8, written as prose.
- `docs/troubleshooting.md` — common Salesforce error codes an implementer will hit (`INVALID_SESSION_ID`, `REQUEST_LIMIT_EXCEEDED`, `INSUFFICIENT_ACCESS_OR_READONLY`, etc.) and what to do about each.
- `CHANGELOG.md` following the monorepo's Conventional Commits + changelog generation convention.
- Mark any AI-drafted policy/compliance-adjacent doc (e.g. `docs/security.md`) with **"AI-assisted draft — pending legal/compliance review"** until a human has signed off, per internal documentation policy.

---

## 11. Example automations to include in demo/docs

Give implementers working starting points, same spirit as the Deploy provider's documented example flows:

- Trigger: **Record Created** (Lead) → Action: **Send Slack message** (if Slack package also installed) to `#sales` "New lead: {Name} from {Company}" — demonstrates cross-provider composition.
- Trigger: **Opportunity Stage Changed** → target stage "Closed Won" → Action: **Publish Content** (Core) to update a public case-studies list, or **Send Email (Salesforce)** to the account owner.
- Trigger: **Umbraco Forms "Form Submitted"** (if Forms add-on present) → Action: **Upsert Record** (Lead/Contact) in Salesforce keyed by email — the classic "web form to CRM" flow, matching the Forms+Slack example already documented for the platform.
- Trigger: **Case Status Changed** to "Closed" → Action: **Send Email** (Core) to the submitting Umbraco Forms contact with a satisfaction survey link.

---

## 12. Definition of done

- [ ] **Umbraco Automate (core) and Umbraco.Automate.OpenIddict are declared package/project dependencies**, and the package fails to compile (not "fails at runtime") without them — no defensive runtime check needed, per §0a.
- [ ] Builds against the exact target framework/Umbraco version pinned in the monorepo, with zero manual steps beyond `dotnet build`.
- [ ] Installs into a clean Umbraco 17 site via NuGet with zero code changes required from the implementer.
- [ ] Connection type, actions, and triggers register purely via attributes (`[ConnectionType]`, `[Action]`, `[Trigger]`) — no manual composer registration call written for them, per §0a.
- [ ] Migrations run automatically via the OpenIddict-style persistence pattern (§5); site boots clean on first install and on upgrade from a previous version.
- [ ] Connection setup is OAuth-only, PKCE where supported, tokens encrypted at rest, re-auth flow works, and reuses OpenIddict's generic connection picker rather than custom UI (§0a, §2).
- [ ] v1 actions in §7 implemented, documented, and covered by tests per §9, with plain text object/field inputs (no live picker).
- [ ] v1 triggers in §6 implemented on top of a purpose-built CDC/Pub-Sub or polling background service (net-new infrastructure, scoped and reviewed on its own — see §6).
- [ ] Security checklist in §8 fully checked off.
- [ ] Docs in §10 published, including the open item below resolved and reflected in `PackageProjectUrl`/marketplace metadata.
- [ ] Structurally consistent with the real `Umbraco.Automate.OpenIddict` project split (for persistence/packaging) and the real `Umbraco.Automate.Slack` action/connection pattern (for the pieces Slack actually has) — not a guess at either, verified against source.
- [ ] No secrets, customer data, or org-specific identifiers ever committed to source control or sample config.

**Open item to resolve before finalizing packaging metadata:** will this package live as a standalone repo long-term, or eventually fold into the `umbraco/Umbraco.Automate` monorepo (the way Slack lives there today)? This affects whether `PackageProjectUrl`, the Marketplace listing URLs, and the repo's own cross-links to Core/OpenIddict should point at this standalone repo or a folder path inside that monorepo. Don't finalize those URLs until this is decided — flag it rather than guessing.

---

## 13. When you (the AI agent) get stuck

1. Re-read the relevant section of `Umbraco.Automate.Slack/CLAUDE.md` and `Umbraco.Automate.OpenIddict/CLAUDE.md` and the actual source for the equivalent concept (connection type, action, trigger, migration). Copy the *pattern*, not literal Slack/OpenIddict strings — and use OpenIddict as the primary structural reference for anything involving persistence or project layout, Slack as the primary reference for connection/action shape (§0a).
2. Check `docs/engineering-spec.md` in the monorepo for the platform contract you're implementing against.
3. Check current Salesforce documentation for anything API-version-specific (OAuth scopes, REST endpoints, CDC/Pub-Sub setup) — Salesforce API versions and best-practice guidance change multiple times a year, so verify current before hardcoding version numbers or endpoint shapes.
4. If the real source and this brief disagree, the real source wins — but **update §0a with the correction** before proceeding, so the next session doesn't rediscover the same fact from scratch.
5. If something here is ambiguous or missing (e.g. exact persistence project naming, exact attribute usage), don't guess silently — note the assumption made and where in the source it was inferred from, so a human reviewer can confirm. Add it to §0a as a pending/unconfirmed item if it's significant enough to affect other sections.