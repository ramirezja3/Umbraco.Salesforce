# Security

> **AI-assisted draft — pending legal/compliance review.** This document was drafted by an AI coding agent as part of building this package. It has not been reviewed by legal or compliance staff. Do not treat it as a compliance guarantee until a human has signed off.

## Secrets and credentials

The Salesforce Connected App's Client Secret lives only in configuration (`appsettings.json`, environment variables, or a secret store) — it is never logged, and this package's own code never writes it to the automation Run history. Access and refresh tokens are encrypted at rest using the same ASP.NET Data Protection mechanism `Umbraco.Automate.OpenIddict` already uses for every other provider's credentials; this package does not implement its own encryption. All Salesforce API calls go over HTTPS; TLS validation is never disabled anywhere in this package's HTTP client configuration.

## Injection

Every action in this package targets a fixed, named Salesforce object (Lead, Contact, Opportunity, CampaignMember, Task) through named, typed fields placed into a structured JSON request body — never a free-text object name, and no raw SOQL anywhere in this package's surface. There is no injection surface to defend against: a bound value (e.g. `${ trigger.Email }`) becomes one JSON field's value, never text concatenated into a query or path.

## Trusted instance URL (SSRF / token-relay defense)

Every Salesforce API call this package makes attaches the connection's live OAuth Bearer access token to a request built against `instance_url` — the organization's API base, captured from Salesforce's own token response at authentication time and stored in the credential record (see Secrets and credentials above). `ISalesforceConnectionResolver` validates, on every resolution, that this stored value is an HTTPS `*.salesforce.com` or `*.force.com` host before ever handing it back to an action or the shared REST client — a connection whose stored instance URL fails that check is treated as unresolvable (the same "reconnect this connection" failure path as an expired token) rather than trusted. This is defense in depth, not a response to an exploited weakness: under normal operation `instance_url` only ever comes from Salesforce's own token endpoint, but nothing this package's own code controls should ever be able to redirect a live Salesforce access token to an arbitrary host purely because a stored value happened to parse as *some* URL.

## Rate limiting and abuse

Every outbound call goes through a shared client that backs off on Salesforce's `REQUEST_LIMIT_EXCEEDED` and HTTP 429 responses, honoring Salesforce's `Retry-After` header when present and falling back to exponential backoff with jitter otherwise, bounded by a configurable maximum attempt count. This prevents one runaway automation from immediately exhausting the organization's daily API allocation on repeated hammering, though it cannot prevent a poorly designed automation from eventually using up a real daily quota — that's a design/monitoring concern for the implementer, not something a client library can fully solve.

## Session recovery

Salesforce's Web Server OAuth flow returns no `expires_in`, so the locally tracked token expiry never proactively triggers a refresh. When Salesforce itself rejects a call because the underlying session has gone stale (org session-timeout policy, revocation, an IP-restriction change), this package recognizes both shapes Salesforce uses to report that condition — the REST Data API's `INVALID_SESSION_ID` JSON error and the identity/userinfo endpoint's plain-text `Bad_OAuth_Token` — and forces a token refresh and retries the call once before giving up. If the refresh token itself has been revoked or expired, the implementer is shown a clear "reconnect this connection" message rather than a raw error, and does need to re-authenticate interactively; that step cannot be automated on the implementer's behalf, by design — an implementer's Salesforce login must remain their own action.

## Permissions

Salesforce connections are Umbraco Automate connections like any other — they are scoped to Workspaces and follow Umbraco Automate's own workspace-permission model. A backoffice user without access to a workspace cannot see or use the Salesforce connection(s) available to it. This package does not introduce a parallel permission model of its own.

## Data handling and GDPR

This package keeps no persistence or local state of its own at all — every action calls the Salesforce REST API directly and returns. Salesforce record fields flowing through actions (which frequently *do* include personal data — names, emails, phone numbers) pass through this package only for the duration of a single automation run; nothing is cached or persisted by this package beyond that run. Given the Danish/EU context this package is likely to be used in: every action in this set *writes* personal data into Salesforce (Create Lead, Create/Update Contact, Create Opportunity, Add to Campaign, Log Engagement Activity) — none of them delete or read it back out. A data-erasure request needs to be handled directly in Salesforce; this package doesn't currently offer a delete/anonymize action.

## Threat model summary

| Threat | Mitigation |
|---|---|
| OAuth token theft | Tokens encrypted at rest via the platform's Data Protection mechanism; never logged; never exposed in the Run history UI. |
| Injection | Not applicable — every action writes named, typed fields to a fixed object; there is no free-text object name or raw SOQL anywhere in this package (see Injection above). |
| Token relay to an untrusted host via a corrupted/spoofed instance URL | `ISalesforceConnectionResolver` rejects any stored instance URL that isn't an HTTPS `*.salesforce.com`/`*.force.com` host before it's ever used to build an authenticated request (see Trusted instance URL above). |
| Session/credential compromise via a stale or leaked token | Encrypted storage; automatic refresh-and-retry on detected session staleness; a clear "reconnect" path (requiring the implementer's own interactive login) when a refresh token itself is no longer valid. |
| Over-broad OAuth scopes | The OAuth registration requests only `api` and `refresh_token` by default; an implementer can broaden this via `Umbraco:Automate:Providers:Salesforce:Scopes` if a future need arises, but nothing beyond the default is requested unless configured. |
| A runaway automation exhausting the org's API allocation | Backoff-with-jitter and a bounded retry count on rate-limit responses; ultimately a monitoring/design responsibility of whoever builds the automation, not something a client library alone can fully prevent. |

## Dependency vulnerabilities

`dotnet list package --vulnerable` against this package's own real (non-project-reference) dependency graph reports several known advisories — all in transitive dependencies of `Umbraco.Automate.Core`/`Umbraco.Automate.OpenIddict` themselves (`MessagePack`, `Microsoft.OpenApi`, `OpenTelemetry.Api`, `SQLitePCLRaw.lib.e_sqlite3`, `System.Security.Cryptography.Xml`), not anything this package references or calls directly. This package does not pin or override those transitive versions — that is a decision for whoever maintains `Umbraco.Automate.Core`/`Umbraco.Automate.OpenIddict`'s own dependency pins, not something to silently override from a downstream provider package. Before installing into a security-sensitive environment, re-run `dotnet list package --vulnerable --include-transitive` against the exact `Umbraco.Automate.Core`/`Umbraco.Automate.OpenIddict` versions actually resolved, since the set of advisories shifts as those packages release new versions.

## What this package does *not* do

- It ships no triggers — nothing in this package fires an automation off a Salesforce-side event. It is a write-only action library (create/update, never read or delete): your automations reach into Salesforce, not the other way around.
- It does not validate an inbound webhook signature, because it does not ship a Salesforce-facing inbound webhook endpoint — Salesforce Outbound Message support was considered and removed from this package's scope (see the project's own change history); a website receiving unsolicited inbound Salesforce callouts is not one of the use cases this package targets.
- It does not implement its own encryption, token storage, or OAuth client — all of that is deliberately delegated to `Umbraco.Automate.OpenIddict`, which is the audited, shared implementation every Automate provider uses.
