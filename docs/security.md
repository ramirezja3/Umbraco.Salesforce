# Security

> **AI-assisted draft — pending legal/compliance review.** This document was drafted by an AI coding agent as part of building this package. It has not been reviewed by legal or compliance staff. Do not treat it as a compliance guarantee until a human has signed off.

## Secrets and credentials

The Salesforce Connected App's Client Secret lives only in configuration (`appsettings.json`, environment variables, or a secret store) — it is never logged, and this package's own code never writes it to the automation Run history. Access and refresh tokens are encrypted at rest using the same ASP.NET Data Protection mechanism `Umbraco.Automate.OpenIddict` already uses for every other provider's credentials; this package does not implement its own encryption. All Salesforce API calls go over HTTPS; TLS validation is never disabled anywhere in this package's HTTP client configuration.

## Injection

SOQL inputs built by this package's own actions (Get Record, Delete Record, Upsert Record) are parameterized by construction — object/field/Id values are placed into the request path or a structured JSON body, not concatenated into a query string. The one place a free-text query is genuinely user/automation-author-supplied is the Query Records (SOQL) action's query field. Umbraco Automate's `${ }` binding substitution happens before this package's code ever sees the settings value, so an action has no hook to escape a bound value inline — this package therefore cannot make an arbitrary bindable SOQL field fully injection-safe by itself. What it does do: reject queries that don't start with `SELECT`, reject a literal semicolon, and enforce a row cap regardless of the query's own `LIMIT`. A helper (`SalesforceSoqlEscaper`) is available for automation authors who build a `WHERE ... = '${ binding }'` clause themselves and need to escape the bound value — see [docs/actions.md](actions.md)'s note on Query Records.

## Rate limiting and abuse

Every outbound call goes through a shared client that backs off on Salesforce's `REQUEST_LIMIT_EXCEEDED` and HTTP 429 responses, honoring Salesforce's `Retry-After` header when present and falling back to exponential backoff with jitter otherwise, bounded by a configurable maximum attempt count. This prevents one runaway automation from immediately exhausting the organization's daily API allocation on repeated hammering, though it cannot prevent a poorly designed automation from eventually using up a real daily quota — that's a design/monitoring concern for the implementer, not something a client library can fully solve.

## Session recovery

Salesforce's Web Server OAuth flow returns no `expires_in`, so the locally tracked token expiry never proactively triggers a refresh. When Salesforce itself rejects a call because the underlying session has gone stale (org session-timeout policy, revocation, an IP-restriction change), this package recognizes both shapes Salesforce uses to report that condition — the REST Data API's `INVALID_SESSION_ID` JSON error and the identity/userinfo endpoint's plain-text `Bad_OAuth_Token` — and forces a token refresh and retries the call once before giving up. If the refresh token itself has been revoked or expired, the implementer is shown a clear "reconnect this connection" message rather than a raw error, and does need to re-authenticate interactively; that step cannot be automated on the implementer's behalf, by design — an implementer's Salesforce login must remain their own action.

## Permissions

Salesforce connections are Umbraco Automate connections like any other — they are scoped to Workspaces and follow Umbraco Automate's own workspace-permission model. A backoffice user without access to a workspace cannot see or use the Salesforce connection(s) available to it. This package does not introduce a parallel permission model of its own.

## Destructive actions

Delete Record requires an explicit "Confirm Delete" checkbox in its configuration — the step fails validation if it isn't set — and is labelled as destructive in the action picker. There is no way to delete a Salesforce record through this package's actions without that explicit opt-in.

## Data handling and GDPR

This package's own persistence (a single table tracking each polling trigger's last-checked timestamp and a small per-record snapshot used only to detect a stage/status change) stores no personal data beyond whatever Salesforce record Ids and stage/status values are needed to detect the next change — it does not cache field values like names or email addresses. Salesforce record fields flowing through triggers and actions (which frequently *do* include personal data — names, emails, phone numbers) pass through this package only for the duration of a single automation run; nothing is cached or persisted by this package beyond that run. Given the Danish/EU context this package is likely to be used in, GDPR-relevant flows to be aware of when designing an automation: Create/Update/Upsert Record write personal data into Salesforce; Delete Record is the natural fit for a data-erasure request; Query Records can return personal data into later steps, which an automation author should be mindful of when deciding what a later step (e.g. a notification) then does with it.

## Threat model summary

| Threat | Mitigation |
|---|---|
| OAuth token theft | Tokens encrypted at rest via the platform's Data Protection mechanism; never logged; never exposed in the Run history UI. |
| SOQL injection | Structured actions (Get/Update/Upsert/Delete) never concatenate user input into a query. Query Records enforces `SELECT`-only, no semicolon, and a row cap; a documented escaping helper exists for authors who build their own `WHERE` clauses, since this package cannot escape a value that's already been substituted into the settings string before its code runs. |
| Session/credential compromise via a stale or leaked token | Encrypted storage; automatic refresh-and-retry on detected session staleness; a clear "reconnect" path (requiring the implementer's own interactive login) when a refresh token itself is no longer valid. |
| Over-broad OAuth scopes | Every action in this package needs only `api` and `refresh_token` — no action requests a scope beyond what it actually uses. |
| A runaway automation exhausting the org's API allocation | Backoff-with-jitter and a bounded retry count on rate-limit responses; ultimately a monitoring/design responsibility of whoever builds the automation, not something a client library alone can fully prevent. |

## What this package does *not* do

- It does not validate an inbound webhook signature, because it does not ship a Salesforce-facing inbound webhook endpoint — Salesforce Outbound Message support was considered and removed from this package's scope (see the project's own change history); a website receiving unsolicited inbound Salesforce callouts is not one of the use cases this package targets.
- It does not implement its own encryption, token storage, or OAuth client — all of that is deliberately delegated to `Umbraco.Automate.OpenIddict`, which is the audited, shared implementation every Automate provider uses.
