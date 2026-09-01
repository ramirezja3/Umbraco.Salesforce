# Development Notes — Umbraco.Automate.Salesforce

This is the full historical build log and original planning brief for this package —
kept for engineers/agents who need the reasoning behind a specific structural choice or
a platform constraint that took real investigation to uncover. It is **not** required
reading to use or contribute to this package day to day — see the top-level `CLAUDE.md`
for the short orientation brief, and `README.md`/`docs/*.md` for user-facing docs.
This file is intentionally not linked from README.md — it is internal engineering
history, not part of the package's public-facing surface.

---

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
| Triggers can be wired to a Connection the same way action Steps are, so a Salesforce polling trigger can just read "the configured connection" | They can't — confirmed by reading `TriggerConfiguration` (alias + settings dictionary only, **no `ConnectionId` field**) and the `trigger-settings-modal.element.ts` frontend (zero connection-related code, unlike the per-step picker actions get). **Fix implemented:** `SalesforceTriggerConnectionResolver` mirrors Core's own `ActionStepBody.ResolveConnectionByTypeAsync` fallback instead — it looks up the automation's `Workspace.AllowedConnections`, filters to Salesforce-typed connections, and picks the first match (logging a warning, not failing, if more than one exists). No new trigger-settings field, no duplicate "authenticate again" OAuth flow. |
| Core's own Webhook infrastruc­ture (`IWebhookTrigger`/`WebhookTriggerBase<TSettings,TOutput>`) is a real extensibility point a provider package can use to build its own webhook-triggered trigger type (e.g. for Salesforce's classic Outbound Message) | It isn't, in this platform version. `WebhookEndpointController` is hardcoded to Core's own concrete `WebhookTrigger` — it calls `_triggers.GetByAlias<WebhookTrigger>(triggerAlias)` specifically and always produces a fixed `WebhookTriggerOutput {Method, Body, Headers, Query}`. A third-party `IWebhookTrigger` implementation would simply never receive dispatch; nothing routes to it. **Fix implemented:** dropped the planned Salesforce webhook *trigger* entirely. §6's "Webhook / Outbound Message Received" row is instead satisfied by a new **action**, `ParseOutboundMessageAction` — point Salesforce's Outbound Message at an automation using Core's *own* built-in Webhook trigger, bind `${trigger.body}` (the raw SOAP XML string) into this action, which parses it into `ObjectType`/`RecordId`/`Fields`. Needs no connection, no live org, fully unit-tested via a canned XML fixture. |
| Core's internal `AutomateDbProvider`/`AutomateMigrationsAssemblies` helpers (which `Umbraco.Automate.OpenIddict.Core`'s `OpenIddictDbContext` uses to configure its EF Core provider) are reusable by any Automate product's `DbContext` | They're not, for a third-party package. `Umbraco.Automate.Core.csproj`'s `InternalsVisibleTo` list is a **fixed allowlist** naming `Umbraco.Automate.OpenIddict.Core` explicitly — it doesn't include this package. `SalesforceDbContext.ConfigureProvider` therefore inlines its own small SqlServer/Sqlite provider switch instead of calling Core's internal one. `DatabaseConnectionInfo` (connection-string resolution) **is** public and reused as-is — only the provider-switch helper is off-limits. |
| `Umbraco.Cms.Persistence.EFCore`'s `AddUmbracoDbContext<T>(...)` extension lives under `Umbraco.Cms.Core.DependencyInjection` or `Umbraco.Cms.Persistence.EFCore` (the namespaces its own package name would suggest) | It's in **`Umbraco.Extensions`** — confirmed by reflecting on the real assembly after `using Umbraco.Cms.Core.DependencyInjection;`/`using Umbraco.Cms.Persistence.EFCore;` alone produced "no accessible extension method" with the namespace seemingly right. Cost real time to track down; noted here so the next session doesn't re-derive it. The matching overload signature is `AddUmbracoDbContext<T>(IServiceCollection, Action<IServiceProvider, DbContextOptionsBuilder, string, string>, bool shareUmbracoConnection)` — mirrored exactly from `Umbraco.Automate.OpenIddict.Core`'s own `AddPersistence` call. |

**Because Salesforce genuinely needs its own persistence** (the Describe-metadata cache table in §5 — something Slack has no equivalent of), **model this package's project structure on `Umbraco.Automate.OpenIddict`'s split, not Slack's single-project shape**: OpenIddict ships as Core + `Persistence.SqlServer` + `Persistence.Sqlite` + a bundling meta-package that references both. That's the closest real precedent in this codebase for "a provider that needs its own tables," and §2/§9 below have been updated accordingly. The `instance_url` row above turned out not to need this after all — but the polling-trigger checkpoint table (below) genuinely does, so the split is populated now, not just kept empty for a deferred future need.

**Status as of this pass:** connection types (production + sandbox), the OAuth composer/handlers, the shared Salesforce REST client (error mapping, rate-limit backoff with `Retry-After`/jitter), all six CRUD/query actions, `ParseOutboundMessageAction`, and a polling-trigger engine with five concrete triggers — Record Created/Updated/Deleted, Opportunity Stage Changed, Lead Converted — are implemented and building/testing green (34 tests: 28 unit + 6 integration — 3 of the integration tests run live against a real org when local credentials are present, and no-op otherwise). Convert Lead (the action, distinct from the Lead Converted trigger), Add Chatter Post, Send Email, Attach File, and Run Apex REST (§7) are not started. Case Created/Case Status Changed triggers are not started (the polling engine already supports them — same pattern as Opportunity Stage Changed — just not instantiated). Platform Event Received (§6) is not started and can't be — genuine Platform Events aren't queryable after the fact, only visible on a live CometD/Pub-Sub stream, so there's no way to build or verify it without a live org. No custom UI, no docs beyond this file.

**By explicit user instruction, the next batch of triggers/actions was capped at the 5 easiest/highest-value remaining items, deferring the genuinely complex ones.** Planned: the two trivial trigger instantiations (Case Created, Case Status Changed) plus three actions (Add Chatter Post, Convert Lead, Send Email). **Built: 4 of those 5** — Convert Lead turned out not to be feasible as planned; see the correction below. Attach File and Run Apex REST remain explicitly deferred as "complex," not attempted this pass — see the note two paragraphs down.

**Correction — Convert Lead has no REST-only path, so it wasn't built.** The original assumption (a `POST /services/data/vXX.X/actions/standard/convertLead` standard Invocable Action, mirroring `chatterPost`/`emailSimple`) is wrong. Confirmed live: `GET /services/data/v61.0/actions/standard` against the real dev org returns exactly 50 standard invocable actions, and none of them convert a Lead — the only Lead-related entry is `invocableApplyLeadAssignmentRules` (assignment rules only, not conversion). Salesforce's only generally-available way to convert a Lead is the SOAP `convertLead()` call, which CLAUDE.md §3 explicitly forbids ("Do not use the SOAP API"), or a custom Apex class/Flow deployed into the *target org* — which isn't something a NuGet package's REST client can do, and would break non-negotiable #1 ("zero required code from the implementer"). **Decision: Convert Lead is dropped from this pass entirely** (not merely deferred) — it doesn't fit into either "easy" or "just needs more time," it's blocked by a genuine platform gap. If it's ever revisited, the realistic options are (a) accept a SOAP dependency for this one action only and get explicit sign-off that it's an exception to §3, or (b) ship an optional companion Apex class in `docs/` that implementers deploy themselves via Setup, which is a real departure from "zero required code" and would need to be documented as such, not silently done.

Built and green this pass: **Case Created**, **Case Status Changed** (triggers — exact mirrors of `RecordCreatedTrigger`/`OpportunityStageChangedTrigger`, pinned to the Case object), **Add Chatter Post**, **Send Email (Salesforce)** (actions). All four registered purely via attributes and confirmed live in the real backoffice canvas — see below. Deliberately deferred as "complex" and not attempted in this pass: Attach File (multipart/base64 `ContentVersion` + `ContentDocumentLink` handling, plus Umbraco media integration) and Run Apex REST (needs the same SSRF/URL-allowlist protections as Core's HTTP Request action — a bigger, security-sensitive lift). Revisit those two once the platform gap in the "connection-type-alias" row above is also revisited, since both are meaningfully harder than everything else in §7.

### Add Chatter Post / Send Email — built on standard Invocable Actions, not the shapes originally assumed, verified live before writing any code

Both ride Salesforce's **standard Invocable Actions REST resource** (`/services/data/vXX.X/actions/standard/{name}`) rather than the dedicated Chatter REST API (`/chatter/...`) or a guessed SOAP-adjacent shape — chosen because it's simpler, already proven to work by live-querying the real org (`ilspycmd`-style probing but against the Salesforce API itself: a throwaway console app using the same Client Credentials Flow as the existing `LiveSalesforce*` test fixtures, reading `.env`, never printing secrets), not because it was the first thing found in search results. Concretely:

- **`chatterPost`** (`AddChatterPostAction`): inputs `text`/`subjectNameOrId` (both required), confirmed via a live `GET .../actions/standard/chatterPost` describe call. Requires only the `api` scope this package already requests — **not** the separate `chatter_api` scope some documentation associates with the dedicated Chatter REST API; that assumption in the original §7 table only applies if a future action uses that other API instead.
- **`emailSimple`** (`SendEmailAction`): live describe on the real org (API v61.0) returned field names — `emailAddresses`, `ccRecipientAddressList`, `bccRecipientAddressList`, `senderType`, `senderAddress`, `emailSubject`, `emailBody`, `recipientId`, `relatedRecordId`, and others — that **do not match** some current public Salesforce documentation, which describes newer/renamed fields (`ccAddresses`, `recipientAddresses`) gated to a later API version (v65.0+) than this org's configured `ApiVersion` (v61.0) actually exposes. Built against the live-confirmed v61.0 shape, not the docs. **Do not "fix" the field names in `SendEmailAction.cs` to match newer doc pages without re-checking live against whatever `ApiVersion` is actually configured** — the docs describe a moving target across API versions, and this is exactly the kind of assumption this corrections log exists to prevent silently drifting on.
- **Error-shape fix, not assumption:** a business-validation failure from either action comes back as an HTTP 4xx with the error nested under `errors[0]` — e.g. `[{"actionName":"chatterPost","errors":[{"statusCode":"UNKNOWN_EXCEPTION","message":"..."}],"isSuccess":false,...}]` — confirmed by deliberately triggering one live (an invalid `subjectNameOrId`, a safe read/validation-only probe — no data was created). This is a **different shape** than the standard REST data API's `[{"message":...,"errorCode":...}]` array that `SalesforceErrorMapper` already handled — extended it with a new branch rather than assuming the existing one would just work. Regression-tested in `SalesforceErrorMapperTests` with the exact captured payload.
- **Not live-tested (deliberately):** an actual successful `chatterPost`/`emailSimple` call. Posting to the real user's Chatter feed or sending a real email are both side-effecting, externally-visible actions outside what live-org validation should do without being asked — the input schema and error shape were confirmed live (enough to build correctly against), and the success-envelope shape (`isSuccess:true`, `outputValues: {...}`) is the well-documented, stable mirror of the failure shape that *was* captured live, so it wasn't independently re-verified by actually sending anything.

Unit tests added: `AddChatterPostActionTests`/`SendEmailActionTests` (validation + no-connection failures — matches this codebase's existing scope for action-level unit tests, which stops at pure/local logic and leaves HTTP-calling behavior to the live integration harness, same as every action before these two), plus 6 new `PollingTriggerTests` cases for Case Created/Case Status Changed (baseline-seeding, fires-on-change, target-status filter) mirroring the existing Record Created/Opportunity Stage Changed test shapes exactly, plus one new `SalesforceErrorMapperTests` case for the invocable-actions error shape. All 41 unit tests green (28 previously + 13 new).

**Confirmed live in the real backoffice canvas** (not just unit tests): both `Case Created` and `Case Status Changed` appear under the CRM group in the trigger picker; `Add Chatter Post` renders its "Post To"/"Text" settings form correctly; `Send Email` (Core) and `Send Email (Salesforce)` both appear side by side in the action picker with distinct descriptions, confirming the "distinguish clearly from Core's own Send Email" requirement in §7 actually renders that way to an implementer, not just in code comments.

**Added afterward, by direct user request: `CreateLeadAction`** (`salesforce.createLead`) — not in the original §7 table, added because the generic `CreateRecordAction` (raw JSON field map) is awkward for the single most common write in this package's own §11 example automations ("web form to CRM"). Named fields (`LastName`/`Company` required — matching Salesforce's own default-org requiredness; `FirstName`/`Email`/`Phone`/`Title`/`LeadSource`/`Status` optional) plus an `AdditionalFields` JSON escape hatch for anything else, merged so named fields always win over `AdditionalFields` if both set the same key. This is effectively the REST-native, buildable substitute for the Convert-Lead use case that got dropped above — it doesn't convert an existing Lead, but it covers "get a new Lead into Salesforce," which is what most of these automations actually need. Confirmed live in the real canvas: `LastName`/`Company` show the required-field asterisk, everything else doesn't — the non-nullable-vs-`string?` convention (CLAUDE.md's own established pattern, not a new mechanism) worked exactly as expected. 4 new unit tests (validation + no-connection failures, matching this package's existing scope for action-level tests); 45/45 unit tests green.

### Interactive OAuth flow — validated live via the real backoffice UI (not just Client Credentials Flow), three real bugs found and fixed

Unlike the bullet above (Client Credentials Flow only, no browser), this pass ran the actual "Authenticate with Salesforce" popup button in a real running demo site backoffice, end to end, against the live Developer Edition org referenced in `.env`. This is the first time the real `OAuthChallengeController`/`OAuthCallbackController`/OpenIddict Client pipeline — the code path every real implementer actually uses — was exercised, and it surfaced three genuine bugs, none of which the Client Credentials Flow testing above could have caught (that flow never touches the challenge/callback controllers or the browser popup at all):

1. **Missing OAuth scopes.** `SalesforceComposer`'s `AddSalesforce(...)` calls only set the issuer — no `.AddScopes(...)` — so the real authorize request sent `scope=openid` only, silently dropping `api` and `refresh_token` even though appsettings/docs describe them as required. **Fix:** added `salesforce.AddScopes("api", "refresh_token")` to both registrations (production and sandbox).
2. **`refresh_token` scope ≠ `refresh_token` grant type.** Even after (1), token refresh threw `InvalidOperationException: The specified grant type (refresh_token) has not been enabled in the OpenIddict client options.` the first time an access token expired. Requesting the *scope* only makes Salesforce *issue* a refresh token; OpenIddict's client separately needs the grant type allowed **client-wide** via `options.AllowRefreshTokenFlow()` inside the `.AddClient(options => ...)` callback — the per-registration `salesforce.AddGrantTypes(OpenIddictConstants.GrantTypes.RefreshToken)` builder method (which looks like it should be sufficient, and which this fix also keeps) is **not** sufficient by itself. Confirmed by decompiling `OpenIddict.Client.WebIntegration`/`OpenIddict.Client` locally (`ilspycmd`, installed as a `dotnet tool`) to find the exact reflection surface (`AddScopes`, `AddGrantTypes`, `SetIssuer`, `UseProductionEnvironment` on the per-provider builder; `AllowRefreshTokenFlow`/`AllowAuthorizationCodeFlow`/etc. on the top-level `OpenIddictClientBuilder`) rather than guessing. **Fix:** `options.AllowRefreshTokenFlow();` added once, before the two `AddSalesforce` registrations, in `SalesforceComposer.cs`.
3. **Not a package bug, but a real deployment gotcha worth recording:** a manually-triggered automation run sat stuck in `Running` forever with zero step executions. Root cause was the demo site's own hosting config, not this package — `Umbraco:CMS:WebRouting:UmbracoApplicationUrl` wasn't set, so Core's server-role election never ran, so the Outbox dispatcher never became eligible to consume `umbraco.automate.trigger`/`.workflow.queue`/`.workflow.events` (logged as `Outbox has N pending message(s) but no topics are eligible for processing on this node... ServerRole is Unknown`). Fixed in the demo site's `appsettings.Development.json` (not this package) by setting `Umbraco:CMS:WebRouting:UmbracoApplicationUrl` and `Umbraco:CMS:Global:DisableElectionForSingleServer: true`. **Doc implication for §10:** `docs/installation.md` and `docs/troubleshooting.md` must call this out explicitly for anyone self-hosting a single-instance site — it's an easy, silent trap that has nothing to do with Salesforce specifically but will look exactly like "the package is broken" (a stuck run, no error) to an implementer.

After all three fixes: full round trip confirmed working in the real UI — Save → "Authenticate with Salesforce" popup → live Salesforce login/consent → popup closes, connection shows "Connected" → "Test connection" succeeds (real org ID/username/instance URL back) → a Manual-Trigger automation with a `Query Records (SOQL)` step run via "Run now" completes with status `Completed` against the live org, including a clean automatic token refresh on a later run (verified by triggering it again after the original access token had expired).

**Chrome-autofill gotcha, not a package issue, noted so a future session doesn't lose time on it:** the backoffice login form on this machine repeatedly autofilled a real, unrelated saved credential instead of the demo site's own unattended-install admin (`admin@example.com` / `password1234` in `appsettings.Development.json`). Always clear and retype the actual local demo credentials rather than trusting what's pre-filled.

### Salesforce polling triggers — how they actually fire, since there's no live org to demo it against

Five trigger types (`RecordCreatedTrigger`, `RecordUpdatedTrigger`, `RecordDeletedTrigger`, `OpportunityStageChangedTrigger`, `LeadConvertedTrigger`) implement `ISalesforcePollingTrigger`, evaluated every `Umbraco:Automate:Salesforce:Polling:PollInterval` (default 1 minute) by `SalesforcePollingBackgroundJob` — same MainDom/server-role guard shape as Core's own `ScheduledTriggerBackgroundJob`, same per-automation try/catch-and-continue. Each automation gets its own checkpoint (`SalesforcePollingStateEntity`: last-poll timestamp + an optional recordId→value snapshot for change detection) persisted via the new `SalesforceDbContext`/EF migrations. Deleted-record detection uses Salesforce's dedicated `.../sobjects/{type}/deleted/` endpoint, not SOQL (deleted rows aren't SOQL-visible). All five have a "don't backfill on first poll" rule.

### Live-org validation (a real Developer Edition org, via OAuth Client Credentials Flow for local testing only)

Once local Connected App credentials became available, this package's actual production code — not just hand-checked assumptions — was run against a real org (`tests/.../LiveSalesforce/`: `LiveSalesforceFixture` + `LiveSalesforceCrudTests`, opt-in via a gitignored local `.env`, no-op for anyone without one, so CI and other developers are unaffected). This is genuinely valuable and found one real bug:

- **Confirmed correct, unchanged:** `SalesforceErrorMapper` against a real `REQUIRED_FIELD_MISSING` response; the full Create/Get/Update/Delete round trip end to end through the real `SalesforceClient`; every JSON shape assumed elsewhere in this package (query `totalSize`/`done`/`records`, userinfo's `organization_id`/`preferred_username`, Deleted Records' `deletedRecords`/`id`/`deletedDate`/`earliestDateAvailable`/`latestDateCovered`).
- **Found and fixed a real bug:** `RecordDeletedTrigger` used `JsonElement.GetDateTime()` on the `deletedDate` field, which throws on Salesforce's `+0000` (no-colon) UTC offset format — .NET's strict RFC 3339 parser rejects it, `DateTime.Parse` (used correctly everywhere else in this package, via `SalesforceJsonHelpers`) accepts it fine. Fixed, and a fixture-based unit test (`RecordDeletedTriggerTests`, using that exact real-world date string) now guards against it regressing — this is the reliable way to catch this class of bug, not a live test.
- **A live-org characteristic, not a bug:** Salesforce's Deleted Records index lagged a just-issued delete unpredictably — sometimes visible within 2 seconds, sometimes still absent after 2 minutes and past its own reported `latestDateCovered` watermark, on the same Developer Edition org. `RecordDeletedTrigger`'s query logic and JSON parsing are proven correct (by the fixture-based unit test above); whether a specific deletion is visible *yet* is Salesforce's own eventual consistency, outside this package's control. The live test for this (`RecordDeletedTrigger_PollAsync_CallsAndParsesTheRealEndpointWithoutThrowing`) deliberately asserts only "the real call succeeds and parses," not "the deletion is visible" — a timing assertion against unpredictable external latency would just be a flaky test, which is worse than no live test.
- **Not validated (and can't be, without a running Umbraco backoffice):** the actual interactive Authorization Code + PKCE flow the real connection types use. Client Credentials Flow (server-to-server, no browser) was used for local testing only — it validates the REST data-path code, not the OAuth challenge/callback controllers.

**Credential handling:** `.env`/`.env.*`/`*.env` are gitignored (added this pass — they weren't before, so double-check any local secrets file predates this if cloning an older checkout). `LiveSalesforceCredentials.TryLoad()` never hardcodes a secret; it reads a local, ignored file or `SALESFORCE_TEST_ENV_PATH`, and returns `null` (tests no-op) when neither exists.

### Full actions/triggers audit — findings confirmed and **fixed**

User asked to "go through all the actions and triggers and bugfix them, look for edge cases." Every action file (`CreateRecordAction`, `UpdateRecordAction`, `UpsertRecordAction`, `GetRecordAction`, `DeleteRecordAction`, `QueryRecordsAction`, `ParseOutboundMessageAction`, `AddChatterPostAction`, `SendEmailAction`, `CreateLeadAction`) and every trigger file (`RecordCreatedTrigger`, `RecordUpdatedTrigger`, `RecordDeletedTrigger`, `OpportunityStageChangedTrigger`, `LeadConvertedTrigger`, `CaseCreatedTrigger`, `CaseStatusChangedTrigger`) plus the shared helpers (`SalesforceActionSupport`, `SalesforceClient`, `SalesforceJsonHelpers`, `SalesforceSoqlEscaper`, `SalesforceTriggerSupport`, both connection resolvers, `SalesforcePollingBackgroundJob`) were read end to end this pass. Five concrete, worth-fixing findings came out of it — **all five implemented and tested this pass** (unit suite green at 63/63, integration project builds with 0 errors, demo site rebuilt/restarted clean with both existing automations validating). Findings kept below for the record; each is now marked FIXED with what actually landed.

1. **FIXED — `QueryRecordsAction.ApplyRowLimit` mishandled `LIMIT n OFFSET m`.** The regex (`\bLIMIT\s+(\d+)\s*$`) only matched a bare trailing `LIMIT`, so `... LIMIT 10 OFFSET 5` produced a double-LIMIT (invalid SOQL). Fixed by extending the regex with a named `(?<offset>\s+OFFSET\s+\d+)?` group and preserving it through the replace. Two new test cases added to `QueryRecordsActionRowLimitTests`.

2. **FIXED — every polling trigger swallowed query failures with zero log output, forever.** None of the 7 concrete trigger classes had an `ILogger`. Fixed by adding `ILogger<T>` to all 7 trigger constructors and `LogWarning`-ing `result.Error?.Message` (plus automation/trigger context) whenever `!result.IsSuccess`. Constructor signature change required updating every call site in `PollingTriggerTests.cs`, `RecordDeletedTriggerTests.cs`, and the live-org `LiveSalesforceCrudTests.cs` (all now pass `Mock.Of<ILogger<T>>()`). New regression test: `RecordCreatedTrigger_QueryFails_LogsWarningRatherThanFailingSilently`.

3. **FIXED — silent, permanent data loss when a poll window has more matches than `MaxQueryRows` (default 200) — the most serious finding.** The six affected triggers (`RecordCreatedTrigger`, `RecordUpdatedTrigger`, `CaseCreatedTrigger`, `LeadConvertedTrigger`, `OpportunityStageChangedTrigger`, `CaseStatusChangedTrigger`) unconditionally advanced the persisted watermark to `context.PollStartedUtc` even when the SOQL `LIMIT` was hit, permanently skipping anything beyond the cap. Fixed with a new shared helper, `SalesforceTriggerSupport.ComputeNextPollWatermark(recordCount, maxQueryRows, lastRecordTimestamp, pollStartedUtc)`, which caps the watermark to the last processed record's own timestamp whenever the record count reaches the cap — threaded through all six triggers (each now tracks `recordCount`/its own "last timestamp seen" variable through its loop). `RecordDeletedTrigger` deliberately untouched (its endpoint has no SOQL LIMIT). Residual edge case (identical-millisecond records straddling the boundary) documented in the helper's XML doc, not fully fixable without ID-based pagination — accepted as-is. New tests: `RecordCreatedTrigger_ReturnedRecordCountHitsMaxQueryRows_CapsWatermarkToLastRecordTimestamp`, `RecordCreatedTrigger_BelowMaxQueryRows_AdvancesWatermarkToPollStartedUtc`, `OpportunityStageChangedTrigger_ReturnedRecordCountHitsMaxQueryRows_CapsWatermarkToLastRecordTimestamp`.

4. **FIXED — free-text comma-separated "Fields" settings weren't sanitized before being spliced into SOQL/query strings.** Fixed by adding `SalesforceFieldListHelper.CleanCommaSeparatedList(string? raw)` (new static class in the `Api` namespace) — splits on comma, trims each piece, drops empty entries, rejoins. Applied in `RecordCreatedTrigger`, `RecordUpdatedTrigger`, `CaseCreatedTrigger`, and `GetRecordAction` (which now only appends `?fields=` when the cleaned result is non-empty). New `SalesforceFieldListHelperTests` with 10 `[InlineData]` cases covering null/empty/whitespace/trailing-comma/double-comma/leading-comma inputs.

5. **FIXED (minimal scope, as flagged) — `ParseOutboundMessageAction` only ever parsed the first `<Notification>` in a batch.** Implemented the minimal, safe fix rather than the bigger collection-output redesign: added `ParseOutboundMessageOutput.AdditionalNotificationCount` (int), computed as `allNotificationElements.Count - 1` after collecting every `<Notification>` element rather than just the first. Documented in the output's XML doc that it reflects extra notifications not parsed — the full multi-record redesign remains a flagged, undone, bigger scope change if ever needed. New test: `ExecuteAsync_BatchedOutboundMessageWithMultipleNotifications_ParsesFirstAndReportsAdditionalCount` (3-notification XML fixture, asserts `AdditionalNotificationCount == 2`).

**Post-fix verification, done:** unit suite green (63/63, up from 45 — new cases per above), integration test project builds with 0 errors, demo site rebuilt and restarted clean ("Validating 2 published automations" / "All 2 active automations validated successfully").

**Item found during that same post-fix live verification, unrelated to findings #1–#5 (2026-08-19):** re-running "Salesforce Live Test" against the real org failed with `SalesforceApiException: "The Salesforce session is no longer valid. Reconnect the Salesforce connection."` (server log, `salesforce.queryRecords` step). Confirmed this is **not** a regression from findings #1–#5 — `SalesforceConnectionResolver.ResolveAsync` (the only code in this package on the token path) does nothing but call `IOAuthCredentialsService.GetValidAccessTokenAsync`/`GetCredentialsAsync` on `Umbraco.Automate.OpenIddict`'s own credential service; this package had no token-refresh logic of its own to regress. **This is also positive confirmation that `SalesforceErrorMapper` is working exactly as designed** — it turned a raw `INVALID_SESSION_ID` into the clear, actionable Run-log message §7 calls for, instead of a stack trace. Root-caused and **now self-healing** — see the next two entries.

**Root cause, confirmed by reading `OAuthCredentialsService.GetValidAccessTokenAsync` (`Umbraco.Automate.OpenIddict.Core`) directly:** the refresh path is gated entirely on `credentials.ExpiresUtc` — `if (credentials.ExpiresUtc is null || credentials.ExpiresUtc > DateTime.UtcNow.Add(ExpiryBuffer)) return credentials.AccessToken;` (no refresh attempted). Salesforce's Web Server OAuth flow token response carries no `expires_in`, so `ExpiresUtc` stays `null` forever for a Salesforce connection — this package's tokens are therefore treated as "always valid" locally and *never* proactively refreshed. The only signal that a session has actually gone stale (org session-timeout policy, revocation, IP-restriction change — none of which have a fixed schedule) is Salesforce itself rejecting a call with `INVALID_SESSION_ID`, which is exactly what happened here after an overnight gap. This is not something fixable from this package by tuning a timeout — there is no local expiry to tune.

**FIXED — added a session-refresh-and-retry-once path so this recovers automatically instead of requiring a manual "Reconnect" after every Salesforce-side session invalidation** (this was flagged by the user as impractical, correctly — "users using automate shouldn't have to authenticate overnight after each session"). Implemented entirely through `IOAuthCredentialsService`'s existing public surface, no `Umbraco.Automate.OpenIddict` internals touched:
- `SalesforceConnectionContext` gained a `CredentialsId` field (both construction call sites in `src` and the 3 in `tests` updated).
- `ISalesforceConnectionResolver` gained `ForceRefreshAsync(credentialsId, ct)`. Implementation (`SalesforceConnectionResolver`): fetch the credential via `GetCredentialsAsync`, set `ExpiresUtc = DateTime.UtcNow.AddMinutes(-1)`, `UpdateCredentialsAsync` — this makes the *next* `GetValidAccessTokenAsync` call take the refresh-token branch, since that's the only externally-reachable way to force it without editing OpenIddict itself.
- `SalesforceClient.SendAsync`'s existing retry loop (previously rate-limit-only) now also catches `error.ErrorCode == "INVALID_SESSION_ID"`: forces a refresh via the resolver, swaps in the new `SalesforceConnectionContext`, and retries the same request once. Bounded by a `sessionRefreshUsed` flag so a session that's *still* invalid after refresh (e.g. the refresh token itself was revoked) fails through to the existing clear "Reconnect" message rather than looping.
- New `SalesforceClientTests.cs` (first-ever direct unit tests for `SalesforceClient`, previously only exercised indirectly through action/trigger tests) with a small `FakeHttpMessageHandler`: covers refresh-then-succeed, still-invalid-after-refresh (no infinite loop), and force-refresh-returns-null (refresh token revoked too) cases. 66/66 unit tests green (63 + 3 new).
- **Still requires a genuine manual reconnect** when the *refresh token itself* has been revoked or expired (e.g. the Connected App's Refresh Token Policy, or an admin revoking the session in Salesforce Setup) — that's a real "credentials no longer valid" case, not a recoverable staleness, and correctly surfaces the same clear message as before.

**FIXED (separate, more urgent bug, found live 2026-08-19 when clicking "Authenticate with Salesforce" after the above):** the real interactive OAuth challenge crashed with `InvalidOperationException: A common grant type/response type combination supported by both the client and the server couldn't be negotiated automatically` (`OpenIddictClientHandlers+AttachGrantTypeAndResponseType.HandleAsync`). Root-caused by decompiling `OpenIddict.Client.WebIntegration` (`ilspycmd`, same tool used earlier this session): the per-registration `AddGrantTypes(...)` builder method does `registration.GrantTypes.UnionWith(types)` on a `HashSet<string>` that starts **empty** on a freshly constructed `OpenIddictClientRegistration` (confirmed by decompiling `AddSalesforce`'s registration construction too — no default grant types are set anywhere before the configuration callback runs). The earlier OAuth fix in this log (`salesforce.AddGrantTypes(OpenIddictConstants.GrantTypes.RefreshToken)`, added to fix token refresh) therefore didn't *add* `refresh_token` to an implicit default set — it replaced an effectively-unconstrained/auto-negotiated set with an explicit, `refresh_token`-only one, silently excluding `authorization_code` and breaking the initial "Authenticate" challenge for every fresh connection from that point on. This is a real regression introduced by that earlier fix, not a pre-existing or unrelated issue — it just hadn't been exercised by a fresh "Authenticate" click since it landed. **Fix:** both registrations in `SalesforceComposer.cs` (production and sandbox) now call `salesforce.AddGrantTypes(OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.RefreshToken)`, explicitly allowing both flows. Comment added at the call site (and on `options.AllowRefreshTokenFlow()` above it) explaining the `UnionWith`-on-empty-set mechanism so this doesn't regress a third time. **Re-verified live** — the user re-authenticated with real Salesforce credentials through the actual backoffice UI and the challenge succeeded (confirmed in the server log: full authorization_code → token → userinfo cycle for the real user, `{redacted-email}`, org `{redacted-org-id}`).

**FIXED — a third bug found immediately after the above, live, when the user clicked "Test connection":** got a "Connection test failed — Salesforce rejected the access token (HTTP 403)" popup. Added logging to `SalesforceConnectionType.ValidateAsync` (it had none) and found the real body: `Bad_OAuth_Token`. Switching the call to the fixed login host instead of the org's instance URL (the first hypothesis) made no difference — same error either way, proving the host was never the cause. The real cause: `SalesforceConnectionType.ValidateAsync`/`SalesforceSandboxConnectionType.ValidateAsync` never went through `SalesforceClient` at all — they hand-rolled their own separate `HttpClient` call, so they never got the session-refresh-and-retry fix above. And even if they had, it wouldn't have helped: `SalesforceErrorMapper` only recognized the REST Data API's JSON `INVALID_SESSION_ID` shape, not Salesforce's identity/userinfo endpoint's plain-text `Bad_OAuth_Token` body for the exact same underlying condition (confirmed live — `queryRecords` against the same stale token in the same run self-healed via the refresh-and-retry path and logged "Successfully refreshed OAuth token," while the hand-rolled userinfo call next to it had no such recovery). **Fix, two parts:** (1) `SalesforceErrorMapper.ExtractFirstError` now special-cases the literal body `Bad_OAuth_Token` and maps it onto `INVALID_SESSION_ID`, same as the JSON shape. (2) Both connection types' `ValidateAsync` rewritten to resolve via `ISalesforceConnectionResolver.ResolveAsync` and call `ISalesforceClient.SendAsync("/services/oauth2/userinfo")` instead of a separate `HttpClientFactory`/`AuthenticationHeaderValue` call — so connection validation now gets the exact same force-refresh-and-retry-once recovery every action already has, instead of a weaker parallel path. New tests: `SalesforceErrorMapperTests.Map_BadOAuthTokenPlainTextBody_MapsOntoInvalidSessionId`, `SalesforceClientTests.SendAsync_BadOAuthTokenPlainTextBody_AlsoForcesRefreshAndRetries`. **Re-verified live end-to-end**: "Test connection" now returns "Connection test succeeded — Connected to {redacted-org-id} as {redacted-test-username} ({redacted-instance-hostname})."

### Deliberate re-scoping pass (2026-08-19) — cut from 8 triggers/11 actions down to 1 trigger/7 actions, by direct user instruction

User feedback, verbatim intent: rename the "CRM" canvas group to "Salesforce"; stop abbreviating words like "org" anywhere in user-facing text; and — the substantive one — "clean up the actions and triggers, use ones that are actual use cases a CMS would want to actually connect to Salesforce with, think from the client perspective and a Salesforce admin... you don't need any overly complex triggers and actions... there shouldn't be so much trigger since Umbraco for the most part is triggering Salesforce actions."

**Group rename + "org" de-abbreviation:** `Group = "CRM"` → `Group = "Salesforce"` on every remaining `[Action]`/`[Trigger]`/`[ConnectionType]` attribute. Every `Label`/`Description` string (and, for good measure, XML doc comments) using bare "org" as shorthand for "organization" was rewritten — `SalesforceConnectionSettings`/`SalesforceSandboxConnectionSettings` ("Salesforce Org" → "Salesforce Organization"), both `ConnectionType` descriptions, `CreateLeadSettings`' Lead Source/Status descriptions, `SalesforceErrorMapper`'s rate-limit message, and several doc-comments across `SalesforceClient`/`SalesforceConnectionType`/`ISalesforceConnectionResolver`/`SalesforceConnectionContext`/`SalesforceOAuthHandlers`/`SalesforceUserInfoResponse`. Left alone: `orgfarm-...` (a real domain name, not an abbreviation), `OrganizationId`/`organization_id` (already unabbreviated), and the local variable `var org` in the two `ValidateAsync` methods (an identifier, not displayed text).

**Trigger cut: 7 of 8 removed, keeping only Opportunity Stage Changed** (§6 has the current table and full rationale). Deleted entirely, source + tests: `RecordCreatedTrigger`, `RecordUpdatedTrigger`, `RecordDeletedTrigger`, `LeadConvertedTrigger`, `CaseCreatedTrigger`, `CaseStatusChangedTrigger` (each with their `*Settings.cs`/`*Output.cs`), plus `RecordDeletedTriggerTests.cs` and the corresponding test methods in `PollingTriggerTests.cs`/`LiveSalesforceCrudTests.cs`. Confirmed before deleting that nothing needed updating in `SalesforcePollingBackgroundJob` — it discovers polling triggers purely via `TriggerCollection.GetByAlias` + the `ISalesforcePollingTrigger` interface, no fixed list anywhere, so removing six trigger classes was a pure subtraction. `SalesforceTriggerSupport.ComputeNextPollWatermark`, `SalesforceFieldListHelper`, and the whole polling-state/checkpoint persistence layer are untouched and still needed by the one surviving trigger (and by `GetRecordAction`, for the field-list helper).

**Action cut: 3 of 10 built actions removed** (§7 has the current table and full rationale) — `AddChatterPostAction`, `SendEmailAction`, `ParseOutboundMessageAction` (each with their `*Settings.cs`/`*Output.cs`), plus `AddChatterPostActionTests.cs`/`SendEmailActionTests.cs`/`ParseOutboundMessageActionTests.cs`. `Convert Lead`, `Attach File`, and `Run Apex REST` needed no action since they were already never built. The seven that ship: `CreateLeadAction`, `CreateRecordAction`, `UpdateRecordAction`, `UpsertRecordAction`, `GetRecordAction`, `DeleteRecordAction`, `QueryRecordsAction`.

**Verification:** `dotnet build` on Core clean (0 errors); unit suite green at 44/44 (down from 68 — the removed coverage was for the removed code, nothing else regressed); integration project builds clean; demo site rebuilt and restarted clean ("Validating 2 published automations" / "All 2 active automations validated successfully" — neither "Create Sample Leads" nor "Salesforce Live Test" used anything that got removed). Confirmed live in the real backoffice canvas: the action picker's group header now reads "Salesforce," and searching it lists exactly the seven kept actions — Add Chatter Post/Send Email/Parse Outbound Message are gone from the picker.

§11's example automations were updated to match — the two that depended on removed triggers (Record Created, Case Status Changed) were replaced with ones using what actually ships (Member Saved → Create Lead as the pre-Forms-package stand-in for web-to-lead; a GDPR-deletion example for Delete Record; a dedupe-check example for Query Records).

### Packaging and installability verification pass (2026-08-19) — the biggest open question in this document, actually answered

User asked, directly: "is the package done? can I install this on existing or new Umbraco 17 installs and start my Salesforce automations? what more work has to be done." Everything up to this point in the whole build had only ever been verified via `ProjectReference` inside the demo site's own solution — the *code* had been proven, but "can someone actually `dotnet add package` this onto an unrelated site" had never once been tested. Rather than answer from code-reading alone, this pass actually tried it, and found one real (if narrower than first assumed) packaging issue plus two genuine, previously-unverified confirmations.

**Initial claim, corrected the same pass — don't trust the first read of a `dotnet pack` in isolation.** Packing only `Umbraco.Automate.Salesforce.csproj` alone produces a `.nupkg` containing nothing but the `.nuspec` and the README — no DLLs. Read in isolation, this looks broken. It isn't: `IncludeReferencedProjects=true` on a meta-package is the standard NuGet "umbrella package" pattern — it declares `Umbraco.Automate.Salesforce.Core`/`.Persistence.Sqlite`/`.Persistence.SqlServer` as ordinary NuGet dependencies (not flattened DLLs), which only resolves correctly once those three are *also* packed and published alongside it. Confirmed this actually works by packing all four into a local folder feed and restoring a throwaway consumer project against `Umbraco.Automate.Salesforce` alone — the full graph resolved, including the real `Umbraco.Automate.Core 17.2.0` and `Umbraco.Automate.OpenIddict 17.1.2` (confirmed published on nuget.org by querying `api.nuget.org/v3-flatcontainer/.../index.json` directly, not assumed) plus their own Persistence.SqlServer/Sqlite packages and `Umbraco.Cms.Persistence.EFCore`. The lesson, not just the fact: a "does this look empty" reaction to one project's isolated pack output isn't sufficient evidence either way for a multi-package umbrella design — the real test is a consumer restore against the whole set.

**Real bug found and fixed:** packing any of the four projects with the *default* `UseProjectReferences` (which resolves to `true` whenever the sibling `../Umbraco.Automate` monorepo checkout is present — i.e. always, in this dev environment) bakes the dependency on `Umbraco.Automate.Core`/`Umbraco.Automate.OpenIddict` to whatever local Nerdbank.GitVersioning preview version that sibling checkout happens to compute (e.g. `17.2.1--preview.1.g636b1dc`) instead of the intended published-floor range (`[17.2.0, 17.999.999)`) from `Directory.Packages.props`. A package built that way would restore fine on this machine and fail to restore anywhere else, silently, with no error until someone else tried it. **Fix:** `-p:UseProjectReferences=false` must be passed when packing for real distribution — this isn't new, the flag already existed for exactly this reason (see the csproj comments), but nothing enforced remembering it. Codified into `scripts/pack-release.ps1` (new), which always passes it, mirroring the real monorepo's own `.azure-pipelines/templates/pack-product.yml` pack step (`dotnet pack {product}.slnx --configuration Release --no-build -p:UseProjectReferences=false`) adapted to this repo's four-project layout.

**Genuinely new verification — the biggest one:** built `scripts/install-package-test-site.ps1` (new, mirrors the real monorepo's own script of the same name) and ran it for real: packed all four projects with the fix above, created a **brand-new, separate** `dotnet new umbraco` site (`demos/v17/Umbraco.Automate.Salesforce.PackageTestSite`, distinct from the long-running `DemoSite` this whole project has used until now), installed `Umbraco.Automate` from nuget.org and `Umbraco.Automate.Salesforce` from the local pack output via `dotnet add package` — zero project references anywhere in this site. Result: clean build, clean boot, `Running 7 pending Automate migrations` / `Automate migrations completed successfully` in the log, and — confirmed live in the browser — **Automation → Connections → Create** lists both `Salesforce` ("Connect to a Salesforce production organization") and `Salesforce (Sandbox)`. This is the first time in the life of this package that the actual NuGet-install path, as opposed to the dev project-reference path, has been exercised at all.

**Also new: the SQL Server persistence path, never once tested before this pass, now is.** Every prior migration test in this project's history ran against SQLite (the `DemoSite`'s configured provider) — the `Persistence.SqlServer` project existed and built, but nothing had ever pointed it at a real SQL Server and confirmed the migration actually applies. A local SQL Server Express instance (`localhost\SQLEXPRESS`) was available; created a second fresh site pointed at it (`umbracoDbDSN`/`umbracoAutomateDbDSN` both `Microsoft.Data.SqlClient`, two separate databases), booted it, saw the same `Running 7 pending Automate migrations` / `Automate migrations completed successfully` log lines, and confirmed directly via `sqlcmd` that `umbracoAutomateSalesforcePollingState` actually exists in the resulting database. Both test databases and the test site folder were removed afterward (its config was machine-specific, not portable — unlike `PackageTestSite`, which uses portable SQLite and was kept).

**Credential handling note:** the long-running `DemoSite`'s `appsettings.Development.json` contains a real Salesforce Connected App Client Secret in plain text (needed for live OAuth testing throughout this project) — confirmed this pass, via `git check-ignore` and `git ls-files`, that both `**/appsettings.Development.json` and the whole `demos/` folder are gitignored and untracked, so this was never at risk of being committed. Both new test sites created this pass use placeholder, obviously-fake credential values instead — they only needed to prove composition/migrations succeed, not real Salesforce API access.

**Also shipped this pass, closing out other items from the "what's left" punch list:** `docs/installation.md`, `docs/triggers.md`, `docs/actions.md`, `docs/security.md` (marked "AI-assisted draft — pending legal/compliance review" per the org's own documentation policy), and `docs/troubleshooting.md` — none of which existed before (the `docs/` folder was empty; `README.md` linked to a dead `docs/installation.md`). `azure-pipelines.yml` was added, mirroring the real monorepo's build → test → pack shape, but is **not** wired to any actual Azure DevOps project — it's a defined, versioned contract, not a running pipeline. The security checklist (§8) and Definition of Done (§12) were updated with honest per-item status instead of being left as an unmaintained template of empty checkboxes.

**Still not done, deliberately not attempted this pass:** actually publishing anything to a real feed (nuget.org or otherwise) — that's a decision for the user, not something to do unprompted. Testing an *upgrade* from a previous version (there isn't one — this is still pre-1.0 and has never been published). Wiring the CI pipeline to a real Azure DevOps project. Committing any of this pass's work, or any prior pass's — see §12.

### Senior-engineer bug-hunt pass (2026-08-21) — 6 findings, 5 fixed for real, 1 found to be unfixable within this package and reverted rather than left as a false fix

A dedicated review pass ("go through the package again, from the scope of a senior engineer, to look at any bugs or token refresh functionality") produced 6 findings, all subsequently addressed:

1. **FIXED — `OpportunityStageChangedTrigger`'s `Amount` output was silently `null` for any whole-dollar Opportunity.** Root cause: `fields["Amount"] as double?` — the field's boxed CLR type from Salesforce's JSON is `long` for a value with no decimal point (e.g. `5000`), and the `as` operator never performs a numeric conversion between boxed value types, only a reference-type-compatible cast, so a boxed `long` can never satisfy `as double?` and silently yields `null` instead of throwing or converting. Fixed with a new `SalesforceJsonHelpers.ToDouble(object? value)` handling `null`/`double`/`long`/`int`/`decimal`/`float` explicitly, used in place of the bare `as double?` cast.
2. **FIXED — the trigger's original first-poll design used a hardcoded 1-day lookback, which silently dropped real stage-change events for any Opportunity not otherwise touched in the 24 hours before the automation went live** (no baseline seeded → "no previous value" → the real change event that follows gets swallowed as if it were the record's first-ever observation). Fixed by replacing the 1-day-lookback first poll with an unconditional, paginated seed sweep of *every* Opportunity's current stage (new `SalesforceTriggerSupport.SeedSnapshotAsync`, using `nextRecordsUrl`/`done` pagination), bounded by a new `MaxSeedRows` option (default 50,000, ~25 API calls once) with an honest truncation warning rather than a silent cap — matching this package's "no silent caps" convention.
3. **FIXED — no locking/dedup around concurrent `ForceRefreshAsync` calls**, so two automation steps hitting `INVALID_SESSION_ID` at the same moment could both redeem the refresh token, and a Connected App with a Refresh Token Policy that rotates the token on each use could revoke the loser's token out from under it. Fixed with a per-`credentialsId` `SemaphoreSlim` gate plus a short (5s) "just refreshed" cache so the second concurrent caller reuses the first's result instead of redeeming again — implemented entirely through `IOAuthCredentialsService`'s existing public surface.
4. **FIXED — `SalesforceClient.SendAsync` never disposed the `JsonDocument` it parsed** on every successful API response, so its rented parse buffer was never returned to the shared array pool. Fixed with a `using` block around the parse, cloning the `RootElement` (which copies into an independent buffer) before disposal.
5. **INVESTIGATED, FOUND UNFIXABLE WITHIN THIS PACKAGE, REVERTED rather than left as a false fix.** The original finding: `SalesforceOAuthHandlers.ExtractInstanceUrl` only captured Salesforce's non-standard `instance_url` token-response field on the `authorization_code` grant, never on `refresh_token`, risking staleness after a Salesforce org-instance migration. An initial fix (also handling `refresh_token`) was applied, then disproven by reading `Umbraco.Automate.OpenIddict.Core`'s `OAuthCredentialsService.RefreshAccessTokenAsync` end to end: it calls `AuthenticateWithRefreshTokenAsync` directly (no `HttpContext`, no `AuthenticationProperties`), whose result type exposes only `AccessToken`/`RefreshToken`/`AccessTokenExpirationDate` — nothing on that path ever reads `context.Properties[AccountLabel]` back out. Only the interactive callback (`OAuthCallbackController`, via `HttpContext.AuthenticateAsync`'s `AuthenticationProperties.Items`) ever persists `AccountLabel`. So the fix was a dead no-op — it set a context property during OpenIddict's internal pipeline that nothing downstream ever consumed on a refresh. Closing this for real would require a change inside `Umbraco.Automate.OpenIddict` itself, which CLAUDE.md §0 puts out of scope for this package. **Reverted** `SalesforceOAuthHandlers.ExtractInstanceUrl` back to `authorization_code`-only, with a comment recording exactly why, rather than shipping code that looks like a fix but isn't. Salesforce's `/services/oauth2/userinfo` response (already used by `ValidateAsync`/`SalesforceUserInfoResponse`) was also checked as a possible alternative source for a fresh `instance_url` on every resolve — it doesn't carry one; `instance_url` is only ever present on the OAuth token endpoint response, for either grant type. **Residual, accepted limitation:** if a Salesforce organization migrates to a new instance host, this package's cached `instance_url` (in `AccountLabel`) goes stale until the implementer manually reconnects via "Authenticate with Salesforce" again — documented in `docs/troubleshooting.md`, not silently left undocumented.
6. **FIXED — `CreateLeadAction`'s "named fields always win" merge over `AdditionalFields` used case-sensitive dictionary keys**, so a differently-cased duplicate (e.g. `"email"` in `AdditionalFields` alongside the named `Email` field) wasn't recognized as the same key and both ended up in the outgoing JSON body instead of the named field winning cleanly. Fixed in `SalesforceActionSupport.TryParseFields` by building the fields dictionary with `StringComparer.OrdinalIgnoreCase`.

New unit tests added for #1, #2, #3, and #6 (finding #4's fix — the `JsonDocument` disposal — has no externally observable behavior to assert beyond "still parses correctly," which the existing suite already covers, so no dedicated new test was added for it): 4 new `OpportunityStageChangedTrigger` tests in `PollingTriggerTests.cs` (whole-dollar Amount regression, seed-sweep-ignores-the-old-1-day-window regression, seed-failure-leaves-state-untouched, plus rewriting the `MaxQueryRows`-watermark-capping test to use a subsequent poll instead of `SalesforcePollingState.Initial` now that the first poll takes the seed path); a new `SalesforceJsonHelpersTests.cs` (7 cases covering `ToDouble`'s boxed-type handling); a new `SalesforceActionSupportTests.cs` (4 cases covering `TryParseFields`'s case-insensitive merge behavior, including a JSON payload with its own case-variant duplicate keys); and a new `SalesforceConnectionResolverTests.cs` (3 cases covering `ForceRefreshAsync`'s concurrency dedup, cross-credential isolation, and the missing-credentials path). Full suite green: **61/61** (up from 44 before this pass), full solution build clean (0 errors). Nothing in this pass has been committed, per the standing instruction not to commit until the user says the package is 100% working.

### Structural-consistency audit against the real `Umbraco.Automate.Slack` source (2026-08-21) — because Umbraco plans to ship this officially

User asked directly for a structural-consistency pass against Slack, since Umbraco intends to adopt this package the same way Slack was adopted, and structural consistency is a hard requirement for that process, not a style preference. A read-only audit (comparing real Slack/OpenIddict source file-by-file, not assumptions) found several gaps beyond what §0a had already settled (persistence split, attribute registration, no custom UI, curated trigger/action count — all reconfirmed correct and not re-litigated). Fixed this pass:

- **Root-level files Slack/the monorepo ship that this repo didn't**: added `.editorconfig` (copied verbatim from the monorepo root — this repo has no shared root to inherit it from, being standalone) and `LICENSE` (MIT, Umbraco HQ, matching the monorepo's), and wired `LICENSE` into `Directory.Build.props`'s packing (`<Content Include>`), same mechanism Slack uses. Added `NuGet.config` (package-source pinning to nuget.org + the Umbraco prerelease/nightly feeds) — previously this repo had no explicit source pinning at all, unlike every other product in this family.
- **Cosmetic naming drift**: `README.md`'s title changed from the dotted `Umbraco.Automate.Salesforce` to the spaced `Umbraco Automate Salesforce`, matching Slack's real README title format. `Umbraco.Automate.Salesforce.slnx`'s test-project solution folder changed from `/tests/` to `/Tests/` (capital T), matching `Umbraco.Automate.OpenIddict.slnx` (the real structural precedent for this package's project split) exactly.
- **Marketplace-facing copy was inaccurate**: `umbraco-marketplace-readme.md` described Platform Events and a Convert Lead action — both dropped from scope months ago per the corrections above — rewritten to describe the actual 1 trigger / 7 actions that ship.
- **`CHANGELOG.md` was a one-line stub**, not in the Keep a Changelog format every other package in this family uses (confirmed against `Umbraco.Automate.OpenIddict`'s real `CHANGELOG.md`). Reformatted to match that header shape, with an honest `## [Unreleased]` — fabricating dated entries with fake commit-SHA links (like OpenIddict's real entries have) would have been presenting invented history as real, which this package's own culture explicitly guards against.
- **Missing appsettings.json JSON-schema generation** — a real Slack feature (`UmbracoAutomateSlackSchema.cs` + a `GenerateAppsettingsSchema` MSBuild target + `buildTransitive/*.props`, giving implementers appsettings.json IntelliSense) that this package's `Core.csproj` had scaffolded as an empty `buildTransitive/.gitkeep` placeholder with a "TODO, see Slack's pattern" comment, never actually built. Built for real this pass: `UmbracoAutomateSalesforceSchema.cs` (describing `Umbraco:Automate:Providers:Salesforce`/`:SalesforceSandbox` via the shared `OAuthProviderConfiguration` base Slack's own schema also extends, plus `Umbraco:Automate:Salesforce` and its nested `:Polling` section, matching `SalesforceApiOptions`/`SalesforcePollingOptions` exactly), `buildTransitive/Umbraco.Automate.Salesforce.Core.props`, and the matching `GenerateAppsettingsSchema` target in `Umbraco.Automate.Salesforce.Core.csproj` — copied from Slack's real target verbatim, renamed. Confirmed generating a correct, sane schema on build (`TimeSpan` properties came out as `"format": "duration"` strings, matching NJsonSchema's built-in handling — not something guessed).
- **Real bug found while verifying the above, fixed, and worth flagging to the Umbraco team as a possible latent issue in Slack's own pack pipeline too**: building `Umbraco.Automate.Salesforce.Core.csproj` with `-p:UseProjectReferences=false` (real NuGet packages — the mode `scripts/pack-release.ps1` and the real monorepo's `pack-product.yml` template both actually build+pack with) made `GenerateAppsettingsSchema`'s `JsonSchemaGenerate` task fail: `Could not load file or assembly 'Umbraco.Automate.OpenIddict.Core' ... The system cannot find the file specified.` Root cause, confirmed by inspecting the actual build output folder: `Microsoft.NET.Sdk.Razor` class-library projects (not Web-SDK/executable projects) do **not** copy transitive `PackageReference` DLLs into their own `bin/` output by default (`CopyLocalLockFileAssemblies` defaults to `false` for library projects) — only `ProjectReference`s copy transitively regardless of that setting. So `JsonSchemaGenerate`'s reflection-based load of `$(TargetPath)` found `Umbraco.Automate.OpenIddict.Core.dll` genuinely absent next to `Umbraco.Automate.Salesforce.Core.dll`, even though the real, correctly-versioned package was present in the local NuGet cache (`~/.nuget/packages/umbraco.automate.openiddict.core/17.1.2/lib/net10.0/`, confirmed by listing it directly) — this was never caught by dev-mode builds because dev mode defaults to `UseProjectReferences=true`, and `ProjectReference` output copying masks the gap entirely. **Fixed** by adding `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` to `Umbraco.Automate.Salesforce.Core.csproj`. Verified end-to-end: full `dotnet build -p:UseProjectReferences=false` now succeeds and generates the schema; packed all four projects for real (`Core`/`Persistence.SqlServer`/`Persistence.Sqlite`/meta-package) and confirmed via `unzip -l` that `LICENSE`, `appsettings-schema.Umbraco.Automate.Salesforce.json`, and `buildTransitive/*.props` all land inside the resulting `.nupkg` files. **Because `Umbraco.Automate.Slack.csproj` is the identical `Microsoft.NET.Sdk.Razor` shape with the identical `JsonSchemaGenerate BeforeTargets="Build"` pattern and no `CopyLocalLockFileAssemblies` override anywhere in its own source (confirmed by grep), this same failure mode would very plausibly reproduce on the real Slack package too if its own CI ever built with `UseProjectReferences=false` from a clean state** — this is worth reporting upstream rather than assuming Slack's real CI has already caught it, since nothing in Slack's own repo suggests it has been fixed there.

**Left open, needing a decision (flagged to the user, not silently resolved):**
1. **`umbraco-marketplace.json`'s `DocumentationUrl`/`IssueTrackerUrl` are still literal placeholders** (`"REPLACE_WITH_REAL_..._BEFORE_PUBLISHING"`) — blocked on the still-undecided "standalone repo vs. folded into the `umbraco/Umbraco.Automate` monorepo" question from the packaging-verification pass above, which also blocks `PackageProjectUrl` in `Directory.Build.props`.
2. **No `PackageIcon`/brand asset** — Slack packs `assets/logo-128.png` into every one of its packages; this repo has no equivalent PNG anywhere, and fabricating a Salesforce-branded icon isn't something to do unilaterally (trademark/brand-authorization concerns). `Directory.Build.props` has a comment marking exactly where `<PackageIcon>` and its packing `<Content Include>` go once an asset exists.
3. **`CLAUDE.md` itself is ~104KB of dated AI build-diary, vs. Slack's real `CLAUDE.md` at ~2KB.** `README.md` currently points readers at this file for "the full brief." Whether to trim this down to a Slack-length brief before hand-off (relocating the historical log elsewhere) or leave it as-is is a call for the user/Umbraco to make, not something resolved silently in this pass.
4. **`docs/*.md` files (installation/triggers/actions/security/troubleshooting) exist in this repo; Slack ships none** — Slack relies entirely on external docs.umbraco.com pages plus a minimal README. Not a defect (arguably more helpful pre-adoption), but whoever owns the eventual Salesforce docs.umbraco.com page should treat these files as source material to port over, not their permanent home. `SalesforceConnectionType`'s `SetupDocsUrl` still correctly points at Salesforce's own generic help page rather than a docs.umbraco.com page, since the latter doesn't exist yet — swap it once one does.

---

## 1. Reference material (read this first)

**There is a real, working local clone of the actual monorepo for this — use it, don't rely on memory or guesswork about what any of these packages contain.** It lives at `../Umbraco.Automate` (sibling to this repo's own root), checked out on `v17/dev` with full history (not shallow — Nerdbank.GitVersioning, which Core/OpenIddict both use, needs full history to compute a version and fails outright on a shallow clone). If it's missing, recreate it:

```bash
git clone https://github.com/umbraco/Umbraco.Automate.git ../Umbraco.Automate
cd ../Umbraco.Automate && git checkout v17/dev
```

**Gotcha already hit once, don't re-trip on it:** that clone is the *whole monorepo*, not just the Core product. `Umbraco.Automate.Core.csproj` lives one level deeper than the clone root, under the monorepo's own `Umbraco.Automate/` product folder — i.e. `../Umbraco.Automate/Umbraco.Automate/src/Umbraco.Automate.Core/Umbraco.Automate.Core.csproj`, not `../Umbraco.Automate/src/...`. Same one-extra-level pattern for OpenIddict (`../Umbraco.Automate/Umbraco.Automate.OpenIddict/src/...`) and Slack (`../Umbraco.Automate/Umbraco.Automate.Slack/...`). This package's own `Umbraco.Automate.Salesforce.Core.csproj` already has the right paths in its `UseProjectReferences` block — check there if this ever seems to have drifted, rather than re-deriving it from scratch.

**When in doubt about anything — not just Slack's patterns, but any Core behavior, service signature, or platform constraint — read the actual source in that clone first.** This brief and its own corrections in §0a describe *intent* and *what's been confirmed*; they are not a substitute for reading the code when something new comes up that isn't already covered here. Core (`Umbraco.Automate/Umbraco.Automate/`) is the bigger, more authoritative reference than Slack for anything that isn't a direct connection-type/action pattern — trigger dispatch, workspace/connection services, EF Core persistence conventions, background job base classes, the whole `Umbraco.Automate.Core.Triggers`/`.Actions`/`.Connections`/`.Workspaces` namespace tree. Slack is the template for the narrow slice of things it actually demonstrates (one connection type, one action) — see the bullet below for exactly what that slice is. Don't guess an API shape from either package's `CLAUDE.md` alone when the real `.cs` file is one `Read`/`Grep` call away in the clone.

Before writing any code, pull down and actually read these — don't rely on memory of what they "probably" contain:

- The clone at `../Umbraco.Automate` (see above) — read the root `CLAUDE.md` and `README.md` first for orientation, then go straight to source for anything specific.
- `Umbraco.Automate/Umbraco.Automate/` within that clone — the core package (workflow engine, triggers/actions abstractions, connections, workspaces). Read `Umbraco.Automate/Umbraco.Automate/CLAUDE.md`, then the actual source under `src/Umbraco.Automate.Core/` for whatever service or extension point is in question.
- `Umbraco.Automate.OpenIddict/` within the clone — reusable OAuth client infrastructure (built on OpenIddict Client WebIntegration). Read `Umbraco.Automate.OpenIddict/CLAUDE.md`. **Salesforce OAuth (Web Server / Authorization Code + refresh token flow) should be implemented on top of this, exactly the way Slack's OAuth is**, not with a bespoke OAuth client.
- `Umbraco.Automate.Slack/` within the clone — **the template for the parts of this package that have a direct Slack equivalent** (a single connection type, actions, config-driven OAuth scopes). It is *not* a template for persistence, custom UI, or triggers — Slack has none of those. Read `Umbraco.Automate.Slack/CLAUDE.md` line by line and confirm, rather than assume:
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

If anything below conflicts with what you find in the real Core/Slack/OpenIddict source, **the real source wins** — update §0a with the correction and proceed on the corrected basis. This document describes intent; the actual repo (the clone at `../Umbraco.Automate`) is ground truth for mechanics, for Core just as much as for Slack.

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

## 6. Triggers (curated down to one — see §0a "Deliberate re-scoping pass")

**Only Opportunity Stage Changed ships.** The original brief below listed eight Salesforce-side triggers; after actually building and using this package, the user explicitly re-scoped it down (2026-08-19, see §0a): Umbraco is overwhelmingly the *source* of events in a CMS-to-CRM integration (a website visitor does something → push it to Salesforce), not the destination of them, so a large generic polling-trigger surface doesn't match how a real client or Salesforce admin would actually use this. The one kept is the one flagship pattern that genuinely runs the other direction and has clear CMS value: a deal closing in Salesforce driving a website change (publish a case study, notify the account owner).

| Trigger | Fires on | Key outputs |
|---|---|---|
| **Opportunity Stage Changed** | An Opportunity's `StageName` changes, optionally filtered to a specific target stage (e.g. "Closed Won") | Opportunity Id, previous stage, new stage, Amount, Account Id, Owner |

Implementation notes (still accurate for the one surviving trigger):
- Polling-based (`ISalesforcePollingTrigger`, `SalesforcePollingBackgroundJob`) — CDC/Pub-Sub would be the near-real-time alternative but isn't buildable/testable without a live organization; polling is the documented fallback, checked every `Umbraco:Automate:Salesforce:Polling:PollInterval`.
- **v1 object/field selection is a plain text input, not a live picker.** No precedent anywhere in this codebase for a live-metadata dropdown (confirmed in §0a) — Slack's equivalent field is plain text too.
- Registered the same attribute-based way as everything else (a `[Trigger(...)]`-decorated class — no manual registration call, no fixed-list wiring anywhere: confirmed `SalesforcePollingBackgroundJob` discovers polling triggers generically via `TriggerCollection`/`ISalesforcePollingTrigger`, so removing the other seven trigger classes required zero changes to it).
- **Removed, and why:** Record Created/Updated/Deleted and Lead Converted were the most generic/technical of the set (pick an arbitrary object + fields) — the least "a specific client use case," the most "a developer building something bespoke." Case Created/Case Status Changed were real but narrower (support-ticket-specific rather than CMS-marketing-specific). Platform Event Received was never buildable without a live org's CometD/Pub-Sub stream (unchanged from the original brief). Webhook/Outbound Message Received was replaced early on by `ParseOutboundMessageAction` riding Core's own Webhook trigger (see §0a) — now also removed, since it's a Salesforce-initiated flow into Umbraco, which is exactly the direction being cut back.

---

## 7. Actions (curated to the practical CMS↔Salesforce write/read set — see §0a)

The original brief below listed eleven actions. After the same 2026-08-19 re-scoping pass, seven ship — the CRUD suite plus the one purpose-built convenience action for the most common real use case (web-to-lead). Convert Lead, Attach File, and Run Apex REST were already never built (see §0a — infeasible without a SOAP dependency, or explicitly deferred as complex); Add Chatter Post and Send Email (Salesforce) *were* built and are now removed as low-value/niche for a CMS integration — a client wiring a website to Salesforce overwhelmingly wants to get form/member data *into* Salesforce as records, not post to an internal Salesforce social feed or relay email through Salesforce instead of Umbraco's own Send Email.

| Action | What it does | Notes |
|---|---|---|
| **Create Lead** | Creates a Lead with named fields for the common attributes (Last Name, Company, Email, Phone, etc.) | The practical, most common write — the web-to-lead conversion this package exists for. Not in the original brief; added by direct user request because the generic Create Record's raw JSON field map was awkward for this single most common case. |
| **Create Record** | Creates a record of any other configured Salesforce object type (Contact, Account, Case, custom objects) with mapped field values | The generic escape hatch for anything Create Lead doesn't cover |
| **Update Record** | Updates a record by Id with mapped field values | |
| **Upsert Record** | Create-or-update by an External ID field | The idempotent, retry-safe write path — prefer this in generated example automations over plain Create |
| **Get Record** | Retrieves a record by Id for use in later steps | |
| **Delete Record** | Deletes a record by Id | Requires an explicit confirmation flag in config — real client use case is GDPR/data-erasure requests from a member portal, not routine automation |
| **Query Records (SOQL)** | Runs a bounded SOQL query and returns matching records | Enforce a max row count/pagination; parameterize inputs, never string-concatenate bound values into SOQL (SOQL injection prevention) — the lookup/dedupe-check path (e.g. "does a Lead with this email already exist") |

Implementation notes:
- Every write action returns the resulting record Id(s) and a success/failure status usable downstream.
- Every action must map Salesforce API errors (`INVALID_FIELD`, `REQUIRED_FIELD_MISSING`, `DUPLICATE_VALUE`, `FIELD_CUSTOM_VALIDATION_EXCEPTION`, governor-limit errors, etc.) into a human-readable message in the Run log, not a raw JSON dump — same UX bar as Slack's action error handling.
- Batch-friendly where sensible (e.g. Create/Update/Upsert should accept either a single mapped record or a collection from a prior step, using Salesforce's Composite/Batch REST API under the hood, not N sequential calls) — this matters for staying under governor and API-call limits.

---

## 8. Security & compliance checklist ("Grade A", no cutting corners)

Updated 2026-08-19 with genuine status per item, not left as an unmaintained template — see §0a's "packaging and installability verification pass" for how several of these were actually confirmed rather than assumed.

- [x] Client secret and tokens never appear in logs, exception messages, or the Run history UI — confirmed by reading every logging call site in the package; none log a token or secret value.
- [x] Tokens encrypted at rest using the platform's existing Data Protection mechanism — reused via `Umbraco.Automate.OpenIddict`'s `OAuthCredentials`, not reimplemented.
- [x] All Salesforce API calls over HTTPS only; TLS validation never disabled — confirmed no `ServerCertificateCustomValidationCallback`/`DangerousAcceptAnyServerCertificateValidator` anywhere in the package.
- [ ] SOQL/SOSL inputs are parameterized/escaped — no injection via bound values. **Partially true, documented limitation, not fully solvable by this package** — see `docs/security.md`'s Injection section: `${ }` binding substitution happens before an action ever sees its settings, so a bindable free-text SOQL field can't be made fully injection-safe from inside the action. `QueryRecordsAction` rejects non-`SELECT`/semicolon queries and caps rows; `SalesforceSoqlEscaper` is an opt-in helper for authors building their own `WHERE` clause. Leaving unchecked rather than claiming this is solved.
- [x] ~~Outbound webhook endpoint... validates request signatures~~ — **N/A, feature removed from scope.** Salesforce Outbound Message support (`ParseOutboundMessageAction`) was cut in the 2026-08-19 re-scoping pass (§0a) — this package ships no inbound webhook endpoint of its own to secure.
- [x] Rate limiting / backoff on Salesforce 429s and `REQUEST_LIMIT_EXCEEDED`, with jitter — implemented and unit-tested in `SalesforceClient` (`SalesforceClientTests`). **No dedicated circuit breaker** beyond the per-call bounded retry — a sustained pattern of failures still gets retried up to the configured attempt count on every call rather than tripping a standing "stop calling entirely" state. Acceptable for v1, worth a follow-up if a real customer's usage pattern needs it.
- [x] Connections are scoped to Workspaces and respect Core's existing permission model — this package introduces no parallel permission model; it reuses Core's Connection-type extension point entirely.
- [x] Destructive actions (Delete Record) require explicit opt-in in config (`ConfirmDelete`, validated) and are labelled as destructive in `docs/actions.md`.
- [ ] Package passes the same static analysis / analyzer ruleset as the rest of the monorepo with zero new warnings — **not independently verified**. Every build this whole project has produced showed only NuGet advisory warnings (NU1902/NU1903, transitive) and zero compiler/analyzer (CA/IDE-prefixed) warnings, which is a good sign, but no deliberate side-by-side comparison against the monorepo's own `.editorconfig`/analyzer ruleset has been done.
- [ ] Dependency versions pinned centrally; no direct Salesforce SDK dependency with known CVEs — **pinning is centralized (`Directory.Packages.props`), but the NU1902/NU1903 advisories on MessagePack/OpenTelemetry.Api/SQLitePCLRaw/System.Security.Cryptography.Xml/Microsoft.OpenApi are real and still present** in every build — they're transitive from `Umbraco.Cms`/`Umbraco.Automate.OpenIddict`'s own dependency tree, not something pinnable from this package without risking a compatibility break by forcing a version the upstream products weren't tested against. Flagging rather than silently accepting or claiming fixed — a human should decide whether to override any of these centrally.
- [x] GDPR-aware defaults given likely Danish/EU customers — documented in `docs/security.md`'s Data handling section. The one sub-item from the original brief that's now moot: the Describe-metadata cache (§5) that was meant to hold only schema, never row data, was never built at all (deferred to v2), so there's no cache to audit for this.
- [x] Threat-model note in `docs/security.md` — written; OAuth token theft, SOQL injection, session/credential compromise, over-broad scopes, and API-quota exhaustion each covered with their mitigation. The original brief's "SSRF via the 'Run Apex REST' action's target URL" entry is now N/A — that action was never built (§0a).

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

Give implementers working starting points, same spirit as the Deploy provider's documented example flows. Updated 2026-08-19 for the curated trigger/action set in §6/§7 — the two examples that depended on removed triggers (Record Created, Case Status Changed) are replaced with ones using what actually ships:

- Trigger: **Umbraco Forms "Form Submitted"** (if Forms add-on present — see §0a, this doesn't exist yet anywhere in the Automate ecosystem) → Action: **Upsert Record** (Lead/Contact) in Salesforce keyed by email — the classic "web form to CRM" flow, matching the Forms+Slack example already documented for the platform. Until a Forms trigger exists, the same pattern works today from **Member Saved** (Core's built-in trigger) → **Create Lead**, binding `${ trigger.Email }`/`${ trigger.MemberName }`.
- Trigger: **Opportunity Stage Changed** → target stage "Closed Won" → Action: **Publish Content** (Core) to update a public case-studies list, or notify the account owner — the flagship CRM-event-drives-the-website pattern, and the reason this is the one Salesforce-side trigger kept in §6.
- A member requests account deletion on the site → Action: **Delete Record** (or anonymize via Update Record) on their matching Salesforce Contact/Lead — the GDPR/data-erasure use case behind keeping Delete Record in §7 despite it being destructive.
- Before creating a Lead from a form submission, Action: **Query Records (SOQL)** to check for an existing Lead/Contact by email, then branch to **Update Record** or **Create Lead** — the dedupe-check pattern SOQL exists for in a CMS context.

---

## 12. Definition of done

Updated 2026-08-19 — most items below are now genuinely verified (not assumed), specifically because this pass built and ran a real fresh-install test rather than trusting the project-reference-based demo site. See §0a's "packaging and installability verification pass" for exactly what was done.

- [x] **Umbraco Automate (core) and Umbraco.Automate.OpenIddict are declared package/project dependencies**, and the package fails to compile without them — confirmed; no defensive runtime check exists, per §0a.
- [x] Builds against the exact target framework/Umbraco version pinned in the monorepo, with zero manual steps beyond `dotnet build` — confirmed, including a from-scratch build using only real NuGet packages (`-p:UseProjectReferences=false`), not just the sibling-project-reference dev path.
- [x] **Installs into a clean Umbraco 17 site via NuGet with zero code changes required from the implementer** — confirmed live, for real, this pass: a genuinely fresh `dotnet new umbraco` site, `dotnet add package Umbraco.Automate`/`Umbraco.Automate.Salesforce` from nuget.org + a local pack output, zero project references. Booted clean, migrations ran, both connection types appeared in the canvas. This was the single biggest previously-unverified claim in this document; it no longer is.
- [x] Connection type, actions, and triggers register purely via attributes — confirmed (and confirmed *again* on the fresh install above, not just the long-running demo site).
- [x] **Migrations run automatically... site boots clean on first install** — confirmed on first install against both SQLite (fresh `PackageTestSite`) and, this pass, a **real SQL Server Express instance** (verified via `sqlcmd`: the `umbracoAutomateSalesforcePollingState` table was actually created). *"...and on upgrade from a previous version"* — **not tested and not yet applicable**: there is no prior released version of this package to upgrade from (still pre-1.0, never published).
- [x] Connection setup is OAuth-only, PKCE where supported, tokens encrypted at rest, re-auth flow works, reuses OpenIddict's generic connection picker — confirmed live earlier this session with a real Salesforce login.
- [x] v1 actions in §7 implemented, documented (`docs/actions.md`, written this pass), and covered by tests per §9.
- [x] v1 triggers in §6 implemented on the polling background service, documented (`docs/triggers.md`, written this pass), and covered by tests.
- [ ] Security checklist in §8 fully checked off — **two items remain genuinely open** (SOQL-binding limitation, unverified analyzer-ruleset parity) — see §8, not silently claiming done.
- [x] Docs in §10 published — `README.md`, `docs/installation.md`, `docs/triggers.md`, `docs/actions.md`, `docs/security.md`, `docs/troubleshooting.md` all written this pass. The `PackageProjectUrl`/marketplace-metadata sub-item remains open — see the note below, unchanged.
- [x] Structurally consistent with the real `Umbraco.Automate.OpenIddict` project split and the real `Umbraco.Automate.Slack` action/connection pattern — verified against source throughout, not guessed.
- [x] No secrets, customer data, or org-specific identifiers ever committed to source control or sample config — confirmed this pass: `appsettings.Development.json` (which briefly held a real Connected App secret during live testing) and the whole `demos/` folder are gitignored and untracked (`git ls-files` confirms); every test-site config created this pass uses placeholder credentials only.

**Still open, not yet done:**
- **Nothing has been committed to git for this entire pass of work** (Create Lead action, the full audit/bugfix pass, the OAuth/session fixes, the actions/triggers re-scoping, and everything in this packaging-verification pass) — ask before committing, per this session's standing instruction not to commit without being asked.
- **Open item to resolve before finalizing packaging metadata:** will this package live as a standalone repo long-term, or eventually fold into the `umbraco/Umbraco.Automate` monorepo (the way Slack lives there today)? This affects whether `PackageProjectUrl`, the Marketplace listing URLs, and the repo's own cross-links to Core/OpenIddict should point at this standalone repo or a folder path inside that monorepo. Don't finalize those URLs until this is decided.
- **CI is defined but not proven to run** — `azure-pipelines.yml` was added this pass (build/test/pack), mirroring the real monorepo's `pack-product.yml` shape, but it isn't wired to any actual Azure DevOps project/service connection, so it has never actually executed anywhere.
- The two open §8 security items above.

---

## 13. When you (the AI agent) get stuck

1. **Go read the actual source in the `../Umbraco.Automate` clone (see §1) — not just Slack's.** Don't confine this to "when confused about a Slack-shaped thing" — Core is the bigger, more load-bearing reference and most of what this package now needs (trigger dispatch, workspace/connection resolution, background job base classes, EF Core persistence conventions) has no Slack equivalent at all, only a Core one. Re-read the relevant section of `Umbraco.Automate/Umbraco.Automate/CLAUDE.md`, `Umbraco.Automate.Slack/CLAUDE.md`, and `Umbraco.Automate.OpenIddict/CLAUDE.md`, then the actual `.cs` source for the equivalent concept (connection type, action, trigger, migration, service interface). Copy the *pattern*, not literal strings — use OpenIddict as the primary structural reference for persistence/project layout, Slack as the primary reference for connection/action shape, and Core as the reference for everything else (§0a).
2. Check `docs/engineering-spec.md` in the monorepo (in the clone) for the platform contract you're implementing against.
3. Check current Salesforce documentation for anything API-version-specific (OAuth scopes, REST endpoints, CDC/Pub-Sub setup) — Salesforce API versions and best-practice guidance change multiple times a year, so verify current before hardcoding version numbers or endpoint shapes.
4. If the real source and this brief disagree, the real source wins — but **update §0a with the correction** before proceeding, so the next session doesn't rediscover the same fact from scratch.

---

## 14. v2 — Actions removed entirely, QA findings triaged (2026-08-26)

A round of manual testing against a real Salesforce Developer Edition org (Forms → Create Lead
worked end to end; the other triggers were separately validated by another session) produced
`SALESFORCE-PACKAGE-BUGS.md`, 12 findings against v0.1.0. Rather than keep growing the action
set, the decision this pass was to **remove all Actions entirely** — this package now ships only
the Opportunity Stage Changed trigger. Everything under §6/§7 above describing the seven actions
(Create Lead, Create/Update/Upsert/Get/Delete Record, Query Records) is superseded by this —
left in place above as historical record of what v0.1.0 shipped, not as current scope.

Concretely, this pass:
- Deleted `src/Umbraco.Automate.Salesforce.Core/Actions/` in full (22 files), plus
  `SalesforceSoqlEscaper.cs` and `SalesforceFieldListHelper.cs` (confirmed zero remaining call
  sites once Actions are gone — the surviving trigger's SOQL is built entirely from internal,
  fixed values, never a bound/free-text value, so there is no injection surface left to escape).
  QA findings #1–3 (SOQL escaping, unguarded `int.Parse` on `LIMIT`, raw JSON parse errors) all
  lived in that deleted code — no fix needed, the surface is gone. Finding #12 (unclear error for
  a bad object name) was about a free-text object-name field only Actions exposed — also moot.
- **Sandbox stays.** It was on the same chopping block initially, but there's no correctness or
  testing benefit to cutting it in this pass — it's just a second OpenIddict registration +
  connection type, untouched by any of this work. Left for a future pass if still wanted.
- Fixed findings #4 (OAuth `Scopes` config was documented but never read — now bound per
  provider via `SalesforceComposer.ResolveScopes`), #7 (`MaxRetryAttempts` was off-by-one from
  its own doc comment), #8 (added `MaxRetryDelay` to cap exponential backoff), #9 (a failed
  forced token refresh was cached for 5 seconds, propagating one transient failure to every
  other concurrent caller — now only successful refreshes are cached), #10 (one genuinely silent
  poll-skip branch in `SalesforcePollingBackgroundJob` now logs a warning), and #11 (network-level
  exceptions in `SalesforceClient.SendAsync` — DNS failure, connection refused, TLS error — now
  map to a clear `SalesforceApiError` instead of propagating as a raw .NET exception).
- Finding #5 (polling watermark can skip same-timestamp records at a poll's `LIMIT` boundary) got
  a deliberately lighter fix than the QA doc's suggested one: instead of a compound cursor
  (timestamp + record Id, which would need a new persisted column and an EF migration in both
  database providers), `ComputeNextPollWatermark` now backs the capped watermark up by one tick,
  so the next poll's `>` comparison re-includes the tied group. Safe with no schema change because
  the trigger already has the guarantees needed: a record whose stage hasn't changed is a silent
  no-op, and any genuine change carries a `TriggerEvent.IdempotencyKey` keyed on
  `recordId:LastModifiedDate`, so a change already dispatched once can't fire twice.
- Finding #6 (no way to target a specific connection when a workspace has more than one
  Salesforce connection) was left as accepted, documented behavior (existing warn-and-pick-first)
  rather than built out as a new connection-picker settings field — nobody has actually hit this
  scenario yet, and it's speculative multi-org UI for a single-trigger package.

The general principle this triage established, worth applying to any future QA-list pass on this
package: question whether a finding's literal suggested fix is proportionate to a real use case
before implementing it. A rare edge case with an existing "good enough, documented" behavior
doesn't automatically earn a schema migration; a stated intent to remove something eventually
doesn't mean removing it in the current pass, if removing it isn't actually required for the
work at hand.
5. If something here is ambiguous or missing (e.g. exact persistence project naming, exact attribute usage), don't guess silently — note the assumption made and where in the source it was inferred from, so a human reviewer can confirm. Add it to §0a as a pending/unconfirmed item if it's significant enough to affect other sections.

---

## 15. v2 reversal — Actions restored, the trigger removed instead (2026-08-26, same day as §14)

§14 above was based on a miscommunicated instruction — the corrected intent is the exact
opposite: **actions are this package's selling point; it should ship no triggers at all.**
Nothing from §14's pass (nor the cleanup pass after it) had been committed, so restoring the
deleted Actions code needed no external reference — `git checkout HEAD -- <path>` recovered it
directly from local history, since `HEAD` still had the original v0.1.0 codebase throughout.

This pass:
- Restored `Actions/` in full (22 files), `SalesforceSoqlEscaper.cs`, `SalesforceFieldListHelper.cs`,
  `SalesforceApiException.cs` (wrongly deleted as "orphaned" during the §14-adjacent cleanup pass
  — it's actually used by `SalesforceActionSupport.cs` once Actions exist), `docs/actions.md`, and
  the five action-only test files.
- Fixed, on the restored code, the QA findings that are actually mechanically fixable: **#2**
  (`QueryRecordsAction.ApplyRowLimit` now uses `int.TryParse` and throws a catchable
  `FormatException` instead of leaking a raw `OverflowException`), **#3**
  (`SalesforceActionSupport.TryParseFields`'s JSON error now names the expected shape and echoes
  the bad input instead of surfacing the raw parser message), and **#12** (`Failed(result,
  objectApiName)` now names the configured object API name on a `NOT_FOUND`, across
  Create/Update/Upsert/Delete Record). **#1** (SOQL injection via unescaped bound values in Query
  Records) is *not* fully fixable — the action only ever sees the SOQL text after Automate's
  `${ }` substitution already ran, so there's no hook left to escape a value that's already been
  spliced into flat text. This is the same structural limitation v1's `docs/security.md` already
  documented; restoring that documented-but-imperfect state is not a regression.
- **Deleted the trigger and its entire persistence layer** — not just `Triggers/`, but
  `Persistence/`, both `Umbraco.Automate.Salesforce.Persistence.SqlServer`/`.Sqlite` projects, and
  the composer's `AddUmbracoDbContext`/migration-notification wiring. The reasoning chain: the
  *only* reason this package was ever split into 4 projects (meta-package + Core + two persistence
  providers) was the trigger's one EF Core checkpoint table. Once the trigger's gone, actions need
  none of it — they call the REST API directly and keep no state — so the 4-project split's own
  stated justification (§0a, "unlike Slack, this package needs its own persistence") no longer
  holds.
- **Collapsed the solution from 4 projects to 1**, matching `Umbraco.Automate.Slack`'s shape
  exactly: merged the old `Umbraco.Automate.Salesforce.Core` project's contents (its C# namespaces
  were already `Umbraco.Automate.Salesforce.*`, never `.Core.*`, so this was a pure file move, zero
  code changes) into `src/Umbraco.Automate.Salesforce/`, deleted the old thin meta-package
  `.csproj` and both persistence projects, dropped the now-unused `Umbraco.Cms.Persistence.EFCore`
  family and `Microsoft.EntityFrameworkCore.Design`/`Microsoft.Data.Sqlite` pins from
  `Directory.Packages.props`, and updated `Umbraco.Automate.Salesforce.slnx`, both test projects'
  `ProjectReference`s, `scripts/pack-release.ps1`, `scripts/install-package-test-site.ps1`, and
  `azure-pipelines.yml` accordingly.
- Sandbox stays untouched throughout (unaffected by any of this — it was never on the table this
  time).
- Build green, 67/67 unit tests + 2/2 integration tests pass after the full sequence above.

**Note for whoever reads this next:** during this pass, `tests/.../Umbraco.Automate.Salesforce.Tests.Unit.csproj`,
`tests/.../Umbraco.Automate.Salesforce.Tests.Integration.csproj`, and this repo's own `.slnx` were
found already missing their `<ProjectReference>`/`<Project Path>` entries to the old Core project
— from *before* this pass started, cause undetermined (not a deliberate edit by this pass or the
one before it). Re-added correctly pointing at the merged project as part of this work; if project
references go missing again after a `dotnet build`/`dotnet test` run, that's worth investigating
as a real tooling issue rather than assuming it's always a stray manual edit.

---

## 16. Generic Record actions replaced with 6 named, deterministic ones (2026-08-26, same day as §15)

The generic action set (Create/Update/Upsert/Get/Delete Record + Query Records SOQL) required a
non-dev automation author to know a Salesforce object API name, and often raw field names or
SOQL — a dev-shaped design. `CreateLeadAction` was always the exception: named fields, a fixed
target object, no guessing. This pass generalizes that pattern to 5 more actions chosen around
what a Commerce/Engage site actually needs to hand to Salesforce, and deletes the 6 generic
actions entirely. Final set: **Create Lead** (unchanged), **Create/Update Contact**, **Create
Opportunity**, **Update Opportunity Stage**, **Add to Campaign**, **Log Engagement Activity**.

**Convert Lead was considered and dropped — re-confirms an existing finding, doesn't overturn
it.** A fresh round of research this pass (WebSearch/WebFetch against developer.salesforce.com,
independent of the original investigation referenced elsewhere in this file) again found no
documented REST standard invocable action for Lead conversion — the REST API Developer Guide's
own site search returns nothing for "convertLead," `GET /services/data/vXX.X/actions/standard`'s
documented example list doesn't include it, and third-party sources agree the only paths are the
legacy SOAP `convertLead()` call or a custom Apex `@InvocableMethod` deployed into the target
org's own Setup — either way violating this package's REST-only / zero-implementer-code
non-negotiables. User confirmed: drop it, ship the other 6, rather than take a SOAP exception or
require implementer-side Apex.

**Field requirements below are verified against Salesforce's Summer '26 / API v67.0 object
reference docs, not assumed** (this matters — see the "always verify current API specifics"
guidance elsewhere in this file):
- **Opportunity**: only `Name`, `StageName`, `CloseDate` are platform-required on create.
  `AccountId` is confirmed **Nillable** (not platform-required) — orgs that require it do so via
  their own validation rule, not core metadata. `CreateOpportunityAction` reflects this: `AccountId`
  is an optional input, documented as "some organizations require this."
- **CampaignMember**: requires `CampaignId` plus exactly one of `ContactId`/`LeadId` (both
  Nillable at the schema level; the platform enforces "exactly one" as a business rule, not a
  schema constraint — confirmed via the object reference's description text, which says
  "Required" on both fields despite the Nillable flag, the classic mutually-exclusive-required
  pattern). `Status` is confirmed Nillable — omitting it lets Salesforce fall back to whatever
  member status the Campaign's own Setup configuration marks as default, so no hardcoded default
  was needed in `AddToCampaignAction`. Note: a newer org setting ("Accounts as Campaign Members")
  can make `AccountId` a third valid option here — not implemented, since Contact/Lead covers the
  stated Commerce/Engage use cases and adding it would reintroduce a three-way exclusivity check
  for a scenario nobody asked for.
- **Task** (`LogEngagementActivityAction`): `WhoId` confirmed genuinely polymorphic across
  Contact and Lead (one field, not two, unlike CampaignMember). `Subject` is confirmed **not**
  platform-required (no "Required" prefix in the object reference, and Nillable) — this action
  requires it anyway as its own product decision, since a blank logged activity is bad UX
  regardless of what the raw API permits. `Status` is required at the schema level but
  "Defaulted on create" (defaults to `Not Started` if omitted) — this action always sends
  `"Completed"` explicitly rather than relying on that default, since a *logged* activity should
  never sit as `Not Started`.
- **Contact**: `LastName` is the only platform-required field; `AccountId` is optional (same
  Nillable pattern as Opportunity). `CreateOrUpdateContactAction` deliberately does **not**
  attempt an implicit "find existing Contact by email" lookup — Salesforce's standard `Email`
  field isn't (and can't be made, since only custom fields support the External ID checkbox) an
  External ID, so there's no REST-native upsert-by-email path, and this package chose not to run
  an internal SOQL lookup to fake one. Create vs. update is explicit via an optional `ContactId`
  setting instead — store the Id an automation's first run returns, bind it back in on later runs.

**Cleanup that followed from the 6 actions going away:** `SalesforceFieldListHelper` (only used
by the deleted `GetRecordAction`) and `SalesforceSoqlEscaper` (already zero real call sites, its
only justification — `QueryRecordsAction`'s free-text SOQL field — gone too) were deleted, along
with their tests. `SalesforceApiOptions.MaxQueryRows` (and its schema mirror) went with
`QueryRecordsAction`, its only reader. `SalesforceActionSupport.Failed`'s `objectApiName`
parameter (added in §15's action-restore pass for the free-text-object-name Record actions'
NOT_FOUND messages) was simplified back to `Failed(result)` with no parameter, since every
surviving/new action targets a fixed, known object — matching `CreateLeadAction`'s original call
shape, which never needed it.

Every new action follows `CreateLeadAction.cs`'s exact shape (same 4-parameter constructor,
same validate → `TryGetCredentialsId` → `TryParseFields` (where an `AdditionalFields` escape
hatch exists) → `ResolveContextAsync` → `SendAsync` → `Failed`/`Success` flow) — this was a
deliberate constraint, not just convenience: the whole point of this redesign was consistency
with the one action already judged "perfect," not a fresh design per action. `AdditionalFields`
was kept on the two actions with substantial field sets (Create/Update Contact, Create
Opportunity) for parity with Create Lead, and dropped on the three narrow, single-purpose actions
(Update Opportunity Stage, Add to Campaign, Log Engagement Activity) where it added no real value.

Build green, 59/59 unit tests + 2/2 integration tests pass after this pass.

---

## 17. Post-v2 release verification (2026-08-28)

With v2 committed and pushed, this pass worked through the release-readiness gaps identified
right after the redesign: cleaned up stale bug-history comments and the superseded
`SALESFORCE-PACKAGE-BUGS.md` (separate commit), then verified the things that can only be
verified by actually running them, rather than re-reading code.

**Live-org action coverage added.** `LiveSalesforceCrudTests` only ever round-tripped a raw Lead
via `SalesforceClient` directly — none of the 6 named v2 actions had been exercised against a
real org. Added `LiveSalesforceActionTests.cs`, which executes the actual Action classes (not
just their underlying REST shapes) via Core's own `Umbraco.Automate.Testing.ActionTestHarness<T>`
— a real `ConfiguredConnection`, real DI, wired to `LiveSalesforceFixture`'s real
`ISalesforceClient`/token. This is a new test-only dependency for this package (`Directory.Packages.props`
gained `Umbraco.Automate.Testing`, pinned to the same `[17.2.0, 17.999.999)` range as Core; the
integration test csproj got the same sibling-checkout-with-NuGet-fallback conditional reference
the src project already uses for Core/OpenIddict). `ConfiguredConnection`'s constructor is
`internal` and this package's test projects aren't in Core's `InternalsVisibleTo` allowlist, so
this harness — not a hand-rolled one — is the only way to construct a real one from outside the
monorepo; worth remembering next time a provider package needs to test through real Action
classes rather than mocking `ISalesforceConnectionResolver`/`ISalesforceClient` entirely.

Results against the real Developer Edition org: **5 of 6 actions confirmed working live**
(Create Opportunity, Update Opportunity Stage, Create/Update Contact — both the create and the
update-by-Id path — and Log Engagement Activity all created/updated the expected record with the
expected field values, then cleaned up after themselves). **Add to Campaign failed** with
`CANNOT_INSERT_UPDATE_ACTIVATE_ENTITY: entity type cannot be inserted: Campaign` — this is the
Client Credentials Flow integration user lacking Campaign create permission in this specific org
(needs the "Marketing User" flag or a Campaign object permission grant in Salesforce Setup), not
a defect in `AddToCampaignAction` — its unit tests already cover the field-mapping/validation
logic, and the live test's own assertions (CampaignMember linking to the right Campaign/Lead) are
correct; they just couldn't run to completion in this org. The test is left in place (it no-ops
without credentials like every other live test, so it can't break anyone else's run) so it starts
passing the moment that org's integration user has Campaign permission. Left as an open item —
see the note at the end of this section.

**Packaged install re-verified against the actual v2 shape**, not the pre-restructure layout
`docs/dev-notes.md` §12 originally validated. `scripts/pack-release.ps1` still packs cleanly
(`Umbraco.Automate.Salesforce.0.1.0.nupkg` — version unchanged, this package has still never been
tagged/released) and `scripts/install-package-test-site.ps1` still installs it into a genuinely
fresh `dotnet new umbraco` site via real NuGet packages (`Umbraco.Automate` from nuget.org,
`Umbraco.Automate.Salesforce` from the local pack output) with zero project references and zero
code changes. Both scripts needed `powershell.exe -ExecutionPolicy Bypass` to run at all on this
machine (the default `Restricted`/`AllSigned` policy blocks unsigned local scripts entirely) —
worth noting in case a CI agent's default policy does the same; the real Azure DevOps `pwsh` task
in `azure-pipelines.yml` may or may not need an explicit bypass depending on the agent's default,
untested since the pipeline has never actually run (see below).

**Backoffice canvas confirmed live, structurally, in the real installed site above** (browser
automation against `https://localhost:44399/umbraco`, unattended-install admin credentials — not
just curling for a 200): both `Salesforce`/`Salesforce (Sandbox)` connection types appear in the
Connections "Create" picker with correct descriptions; a real (unauthenticated) `Salesforce`
connection was created and saved; and once that connection was added to a workspace's **Allowed
Connections**, all 6 actions (Add to Campaign, Create Lead, Create Opportunity, Create/Update
Contact, Log Engagement Activity, Update Opportunity Stage) appeared together under a single
"Salesforce" group in the step picker, with zero Salesforce entries in the trigger picker.

**Genuinely new finding, not previously documented anywhere in this file: an action's
`ConnectionTypeAlias` gates whether it appears in the step picker at all, based on the
*workspace's* `AllowedConnections` — not just which connection a step ultimately binds to.** The
first attempt at this check (a fresh workspace with zero allowed connections) showed **no
Salesforce group whatsoever** in the action picker — not greyed out, not disabled, entirely
absent — which looks exactly like "the package didn't install its actions" to anyone testing
without realizing this platform behavior exists. Confirmed it's the `AllowedConnections` gate
specifically (not, e.g., requiring the connection to be authenticated) by adding an
unauthenticated Salesforce connection to the workspace's `AllowedConnections` and watching the
group appear. **Doc implication:** added to `docs/troubleshooting.md` — this is exactly the kind
of platform (not package) behavior that looks like a bug to an implementer, per this file's own
recurring pattern for documenting those.

## 18. Real interactive OAuth flow completed end to end (2026-08-28, same day as §17)

With a real Connected App's Client ID/Secret added to the package-test site's
`appsettings.Development.json` by the user, this pass completed the one thing §17 flagged as
unverified: the actual `authorization_code` grant through the real backoffice UI, not just Client
Credentials Flow.

- Clicked **Authenticate with Salesforce** on the `Test Salesforce Connection` created in §17 —
  completed near-instantly with no visible login prompt, because this Chrome profile already had
  an active Salesforce session from earlier work on this package (see §0a's "Interactive OAuth
  flow" entry) and Salesforce skipped straight through. **Two dry-run clicks before this one showed
  no effect at all** — turned out to be a coordinate-tracking mistake in the browser-automation
  driver (clicking where the button used to be after a page reload shifted the layout), not a
  package or platform bug; worth remembering that automated UI verification needs to re-locate
  elements after every navigation, not reuse coordinates from a prior screenshot.
- **Saved, then confirmed the credential genuinely persisted** — a hard reload reverted an
  *unsaved* "Connected" state back to "Authenticate with Salesforce" the first time this was
  tried (expected: the UI reflects the in-progress OAuth result before Save writes it to the
  connection record). After Save, a reload kept "Connected," and **Test Connection** succeeded
  against the real org, returning the real org Id and username.
- **Ran the package for real, end to end, through the actual canvas**: built a `Live Verification`
  automation (Manual Trigger → Create Lead) in the same workspace, published it, and used **Run
  now**. The run completed successfully (~1s wall time, ~508ms for the Salesforce REST call) — the
  first genuine "an implementer clicks a button and a Lead appears in Salesforce" confirmation this
  package has ever had, as opposed to a unit test, a live-org fixture test, or a Client Credentials
  Flow probe.
- **Genuinely new finding: a step's Salesforce connection is auto-resolved, not manually picked**,
  when the workspace has exactly one connection of the required type — the `Create Lead` node on
  the canvas showed a bound connection Id under it immediately upon adding the step, with no
  picker UI ever shown. Not tested: what the UX is when a workspace has two connections of the
  same type (e.g. two production Salesforce orgs) — per Finding #6 in
  `SALESFORCE-PACKAGE-BUGS.md` (removed in §15, but the underlying ambiguity was never actually
  fixed) this may still default to "first match" the way the old polling-trigger resolver did;
  worth a follow-up before calling multi-org support fully verified.
- The test Lead the run created (`LastName=IntegrationTest`,
  `Company=Umbraco Automate Salesforce Live Verification`) was queried and deleted from the real
  org afterward via a throwaway Client Credentials script, so nothing was left behind in the user's
  org from this verification pass.

**Add to Campaign's live coverage — closed (2026-08-28).** The user granted the Client
Credentials Flow integration user Marketing User access in this org's Salesforce Setup. Re-ran
`tests/Umbraco.Automate.Salesforce.Tests.Integration` against the live org:
**7/7 passing** (up from 6/7 in §17) — all 6 named actions' live-org action-level tests now pass,
plus the original Lead CRUD round-trip test. All 6 actions are now confirmed working end to end
against a real Salesforce org, not just unit-tested.

**`azure-pipelines.yml` removed (2026-08-28), by direct user instruction.** Checked the real
`Umbraco.Automate.Slack` product folder in the sibling monorepo checkout directly: it carries no
pipeline definition of its own — the monorepo's single `azure-pipelines.yml` lives at the repo
root and drives every product folder through shared `.azure-pipelines/templates/`, a mechanism
this standalone repo was never actually part of. Having a pipeline file that had never once
executed was inconsistent with that precedent, not an improvement on it. If CI is wanted later,
build it against whatever this repo's actual home ends up being (see the still-open
standalone-vs-monorepo question below) rather than a bespoke one-off.

**Still open, genuinely blocked on things outside this session's reach:**
- **Legal/compliance review of `docs/security.md`, the NuGet publish itself, the
  standalone-repo-vs-monorepo decision, and any engagement with Umbraco's own contribution
  process** are process/business decisions, not engineering tasks — explicitly not attempted here.

---

## 19. Heavy POC pass: all 6 actions stress-tested live, one real bug found and fixed (2026-08-28)

By direct user instruction, this pass ran the package "to the fullest" — realistic multi-action
chains, deliberate error conditions, bulk/concurrent usage — against the real org, to surface
anything a genuine implementer would hit. Two infrastructure detours are recorded here briefly
because they cost real time, but the useful output is the findings section below.

**Detour 1 — the Automate canvas UI could not be used this pass.** Browser automation against the
backoffice degraded mid-session (autofill hijacking real saved credentials, then a fully blank
Automate section with zero console/network errors — every API call it made returned 200, every JS
module loaded, nothing painted). Root-caused the *first* symptom to a third-party browser
extension (a password manager) injecting an autofill overlay that Chrome's cross-extension
isolation blocks this automation channel from dismissing — confirmed by an explicit "Cannot access
a chrome-extension:// URL of different extension" error. Reconnecting the browser fixed that
specifically (Content section renders correctly again) but **the Automate section specifically
stayed blank** even after a from-scratch site, fresh database, cleared browser storage, and a
reconnected browser — with genuinely nothing to debug (no error anywhere, every request
succeeded). This is recorded as a real, reproducible environment finding, not chased further per
the user's explicit instruction to stop treating UI problems as blocking. **A scripted
Management-API alternative was also attempted and abandoned deliberately, not because it failed
technically but because it can't work at all here**: this environment redacts OAuth authorization
codes and tokens in-flight at the network layer as a safety measure, including inside this
session's own Node scripts talking directly to localhost — confirmed by seeing the literal string
`[redacted]` (10 characters) come back as the "code" value inside a script that never printed
anything itself. That's a deliberate guardrail against credential exfiltration, not a bug to route
around.

**Detour 2 — the workaround: heavy live-org testing at the REST/action level instead of the
canvas.** Everything below runs through the real `Action` classes via
`Umbraco.Automate.Testing.ActionTestHarness` (see §17) against the real org — it does not exercise
real triggers or real canvas control-flow nodes (If/Switch/ForEach/Parallel as actual steps), since
those specifically require the canvas. New file:
`tests/Umbraco.Automate.Salesforce.Tests.Integration/LiveSalesforce/LiveSalesforcePocTests.cs`.
**All 15 live integration tests pass** (9 from before this pass + this file's 6):

- **Full customer journey, all 6 actions chained live**, each step's output bound into a later
  step exactly as an automation would (`Create Lead` → `Create Opportunity` → `Update Opportunity
  Stage` → `Create/Update Contact` → `Add to Campaign` → `Log Engagement Activity`), then the
  whole chain read back and verified.
- **Bulk (For-Each-style) coverage**: 5 sequential `Create Lead` calls in a loop, confirming no Id
  collisions and no degradation across repeated live calls.
- **Parallel-style coverage**: 5 concurrent `Create Lead` calls via `Task.WhenAll` sharing one
  connection resolver — exercises `SalesforceConnectionResolver`'s real locking behavior under
  genuine concurrent network I/O, not mocked/sequential unit tests. No collisions, no errors.
- **Four deliberate error-path probes** against the real org (nonexistent Opportunity Id,
  nonexistent Campaign Id, a WhoId that's neither a Contact nor a Lead, and the duplicate-rule
  finding below) — all correctly surface as a `SalesforceApiException` rather than succeeding
  silently or leaking a raw exception.

**Real finding #1 (fixed): `DUPLICATES_DETECTED` was unhandled by `SalesforceErrorMapper`.**
Discovered by accident, not designed for: the first draft of the full-journey test used the same
email for both the step-1 Lead and the step-4 Contact, and Salesforce's standard Duplicate Rules
(a distinct mechanism from the already-handled `DUPLICATE_VALUE` unique-field error) rejected the
Contact write with `DUPLICATES_DETECTED: Use one of these records?` — a bare, Lightning-UI-oriented
message that means nothing without the duplicate-record links the real UI would show alongside it.
This fell through to the mapper's generic fallback (`StepRunErrorCategory.Unknown`, message
verbatim). **Fixed**: `DUPLICATES_DETECTED` now categorizes as `Validation` (same bucket as
`DUPLICATE_VALUE`) and humanizes to an actionable message explaining what Duplicate Rules are and
what to do about them. This is a genuinely realistic collision, not a contrived one — a real
"person submits a form as a Lead, later becomes a paying Contact" journey using the same email
across two separate automations would hit this in any org with Salesforce's default Contact
Duplicate Rule enabled, and this package had no documented or handled path for it. New unit test
(`Map_DuplicatesDetected_CategorizesAsValidationWithClearMessage`) and a dedicated live regression
test (`CreateOrUpdateContact_EmailMatchesExistingLead_RealOrganization_DuplicateRuleBlocksWithClearError`)
both confirm the fix. The underlying collision itself is not something this package can prevent —
Salesforce's only suggested resolution (converting the Lead) has no REST-only path, per the
Convert Lead finding in §0a/§16 — so the fix is entirely about surfacing it clearly, not avoiding
it.

**Real finding #2 (documented, not a bug): `Opportunity.StageName` isn't a Restricted Picklist in
this org, and neither this package nor Salesforce validates it.** A test deliberately setting
`StageName` to `"NotARealStageValue"` was expected to fail — instead Salesforce accepted it and
created the Opportunity as-is. This means a typo in an automation's Stage input (e.g.
`"Closed-Won"` instead of `"Closed Won"`) can silently succeed, leaving the Opportunity in a
nonexistent stage with no error to signal the mistake anywhere — not in this package, not in
Salesforce. This is standard, expected Salesforce behavior for a non-restricted picklist (not
every org configures Stage as Restricted), so there's no fix to make here; it's a real,
live-confirmed limitation worth an implementer knowing about. Test renamed to
`CreateOpportunity_InvalidStagePicklistValue_RealOrganization_SalesforceAcceptsItSilently` to
document the actual behavior rather than assert the originally-assumed one.

**Not found: anything wrong with the six named actions' own logic under load or in combination.**
The chained journey, the bulk loop, and the concurrent calls all behaved exactly as designed — no
corruption, no Id collisions, no connection-resolver races, no unexpected partial failures. Within
the surface this pass could actually reach (the REST/action layer, not the canvas), the package
held up under everything thrown at it.

**Genuinely still untested by this pass, because it requires the canvas specifically:** real
trigger-driven execution (Content Published, Member Saved, Webhook, Scheduled), and the real
canvas control-flow nodes (If/Switch/ForEach/Parallel) as actual workflow steps rather than their
code-level equivalents. Worth a follow-up once the Automate section's rendering issue in this
environment is understood or a different environment is available.

### The Automate canvas rendering issue, actually root-caused this time — confirmed as an Umbraco Core frontend bug, not this package's

Asked directly to retry the UI, this pass went past "it's blank with no errors" (§18/§19) into the
actual Lit component tree via `document.querySelector`/shadow-DOM traversal, on a fresh login with
the password-manager autofill conflict from §18 worked around by setting form values directly via
the DOM (`form_input`) instead of simulated keystrokes, which the extension doesn't intercept.

Traced the render tree by hand: `umb-app` → `umb-router-slot` → `umb-backoffice` →
`umb-backoffice-main` → `umb-router-slot` → `umb-section-default` (Automate's own top-level section
renderer, manifest alias `Ua.Section.Automate`). That component's **own reactive state says it has
something to render** — `_sidebarApps` contains exactly one entry
(`UmbracoAutomate.SectionSidebarApp.Settings`) with `_isConditionsPositive: true`, and `_routes` has
2 entries — but its actual light-DOM output is empty (`innerHTML: ""`, 0 children), both on initial
load and after forcing `requestUpdate()` + awaiting `updateComplete` manually from the console. No
exception, no rejected promise, nothing — the component's internal properties say "render this,"
and its `render()` output is nonetheless empty every time.

This is conclusively **Umbraco Core's own `umb-section-default` component failing to translate its
own state into output**, not a manifest problem on this package's side — `UmbracoAutomate.SectionSidebarApp.Settings`
is present in `_sidebarApps` and its condition already evaluated positive, so the extension
registration this package (or `Umbraco.Automate.Core`) contributes is not what's missing. Nothing
about the Salesforce package's own manifest, actions, or connection types is implicated. Given this
is a live+reproducible-on-this-machine Umbraco Core frontend defect (or an interaction between it
and something else in this environment neither this session nor a fresh site/database/browser
could isolate further — see §18), it's out of scope to fix from inside this package, and is
recorded here rather than chased into Umbraco Core's own source. The package's actual behavior
remains verified correct via the 15 live REST/action-level tests in §19, which don't depend on this
component at all.

All test data created during this pass (Leads/Contacts/Opportunities/Campaigns/CampaignMembers/
Tasks, all tagged with a `POC-` prefix) was queried and deleted from the real org afterward —
confirmed zero remaining by a final sweep.

### Correction to the above: it was never a rendering bug — the sidebar group was just collapsed

The "Umbraco Core frontend bug" conclusion above is **wrong**, found while answering a direct
follow-up ask to determine whether a genuinely fresh, minimal install (`Umbraco.Automate` only, no
Salesforce) showed the same blank Automate section. It didn't — a brand-new site with only Core +
Umbraco.Automate rendered the section perfectly on first load. Adding
`Umbraco.Automate.Salesforce 0.1.0` (packed locally, installed via a `nuget.config` local feed) to
that same site then appeared to reintroduce the "blank" symptom on a hard reload / direct URL
navigation — sidebar showed only a "Settings" heading, no children, empty main pane, zero console
errors, zero network failures, zero unhandled rejections (confirmed by installing
`window.onerror`/`unhandledrejection`/`console.error` capture hooks before navigating).

The actual explanation: **"Settings" is a collapsed-by-default sidebar group header, not a stuck or
broken component.** Clicking it expands to reveal "Workspaces" and "Connections", and the main pane
immediately renders the full "Welcome to Umbraco Automate" dashboard — every time, on both a hard
reload and an SPA-internal client-side navigation, with Salesforce installed. What looked like two
different symptoms across this saga (§18's "blank, no errors" and this section's "state says render
but output is empty") was the same one-click affordance, missed repeatedly because nothing in the
empty pane hints that the heading above it is clickable/collapsible.

This means: `umb-section-default`'s reactive state genuinely was correct all along
(`_sidebarApps`/`_routes` populated, conditions positive) — it just hadn't rendered its *expanded*
child list yet, which is expected collapsed-group behavior, not a defect. There is no Umbraco Core
bug here, and there is no Salesforce-package bug here either — the package's manifest, composer, and
sidebar-app registration are all working exactly as intended, on a clean site, with real Salesforce
credentials configured. The entire multi-session "blank Automate section" investigation (§18, §19,
and the section above) was chasing a UI affordance, not a defect. No code changes were needed as a

### Real canvas UI POC, continued on the clean MinimalRepro site — connection editor "Connected" vs. "Test connection" gap

With the rendering mystery resolved, created a real workspace and a real "Salesforce" connection
through the actual backoffice UI on the clean site, then ran the genuine interactive
"Authenticate with Salesforce" popup against the live org (same org/user as earlier passes). Minor,
worth-recording UX inconsistency found immediately after authenticating: the connection editor's
**"Connected" indicator turns green immediately** after the OAuth popup completes, but clicking
**"Test connection" before clicking Save** fails with "No Salesforce account has been authenticated
for this connection." Clicking **Save**, then **Test connection** again, succeeds normally
(confirmed live: `Connected to {redacted-org-id} as {redacted-test-username}`). Read as: the
"Connected" badge reflects the OAuth credential record itself (persisted immediately by the
callback), while "Test connection" resolves the connection through this package's own
`ISalesforceConnectionResolver`, which needs the *connection entity* saved with a reference to that
credential first — a save-ordering gap in the connection editor's UX, not a broken connection. Likely
shared with any other OAuth-based connection type built the same way (Slack included), so not
necessarily a Salesforce-specific defect, but recorded here since this is where it was found. Not
fixed — out of scope for this package if it's Core/editor behavior; worth a one-line callout in
`docs/installation.md` ("Save before testing a freshly-authenticated connection") if this proves
confusing to implementers, but not added yet pending confirmation it isn't already obvious enough.
result of this correction.