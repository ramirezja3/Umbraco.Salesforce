# Changelog - Umbraco.Automate.Salesforce

All notable changes to Umbraco.Automate.Salesforce will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Entries below this line are generated from Conventional Commits history, the same
way `Umbraco.Automate.OpenIddict`'s `CHANGELOG.md` is.

## [Unreleased]

## [0.1.0] - 2026-09-04

Initial public preview.

### Added

- A Salesforce connection type (`login.salesforce.com`), authenticated via OAuth (Authorization
  Code + PKCE) through `Umbraco.Automate.OpenIddict`. Tokens are encrypted at rest using the same
  Data Protection mechanism Core already uses for other providers' credentials. No sandbox
  (`test.salesforce.com`) connection type is shipped.
- Six actions: Create Lead, Create/Update Contact, Create Opportunity, Update Opportunity Stage,
  Add to Campaign, Log Engagement Activity. Each targets one fixed, named Salesforce object
  through typed fields and returns the resulting record Id and success/failure status.
- Automatic rate-limit backoff on Salesforce's `REQUEST_LIMIT_EXCEEDED` / HTTP 429 responses,
  honoring `Retry-After` with jittered exponential backoff otherwise, plus a stale-session
  refresh-and-retry-once on expired access tokens.
- Human-readable Run-log error mapping for common Salesforce API errors (`INVALID_FIELD`,
  `REQUIRED_FIELD_MISSING`, `DUPLICATE_VALUE`, `DUPLICATES_DETECTED`, governor-limit errors).
- Zero-code install: composed automatically via `SalesforceComposer`, fails fast with an
  actionable error if `Umbraco.Automate` core isn't installed/composed.
- Docs: installation guide, action reference, security posture, troubleshooting guide.

### Security

- The stored Salesforce instance URL is validated as an HTTPS `*.salesforce.com`/`*.force.com`
  host before any request is ever built against it, so a corrupted or unexpectedly-shaped stored
  value can't cause a live access token to be sent to an untrusted host.

Compatible with `Umbraco.Cms` 17.4.0–17.6.2 and `Umbraco.Automate` (Core) 17.2.0+, `v18.x`
excluded as a deliberate scope boundary (see `docs/dev-notes.md`).

No triggers are shipped — automations using these actions are driven by Umbraco Automate Core's
own generic triggers (Content Published, Manual, Scheduled, Webhook, etc.).
