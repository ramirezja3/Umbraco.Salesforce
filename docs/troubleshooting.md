# Troubleshooting

## "The Salesforce session is no longer valid. Reconnect the Salesforce connection."

This is Salesforce itself rejecting the stored access token — a session timeout, a revoked session, or an IP-restriction change on the Salesforce side. This package automatically forces a token refresh and retries once when it sees this; if you're seeing this message at all, that automatic recovery already failed, which almost always means the **refresh token** itself is no longer valid, not just the access token. Go to **Automation → Connections → your connection → Disconnect**, then **Authenticate with Salesforce** again.

If this happens repeatedly and shortly after every fresh authentication (not after a long idle period), check your Salesforce org's Connected App **Refresh Token Policy** — if it's set to expire refresh tokens immediately or very quickly, this package (and any other integration) will need to re-authenticate far more often than expected.

## `403` / "Connection test failed" when clicking Test Connection

If the underlying error is `Bad_OAuth_Token`, this is the identity endpoint's version of the session-invalid condition above — same cause, same fix (reconnect).

## `INVALID_SESSION_ID`

The REST Data API's version of "the access token is no longer valid." Same cause and fix as the two entries above.

## `REQUEST_LIMIT_EXCEEDED`

Your Salesforce organization's daily API call allocation is exhausted, or you're hitting a short-term concurrent-request limit. This package retries with backoff automatically; if it keeps happening, check Salesforce Setup's API usage dashboard for the org, and consider whether an automation is running more often, or processing more records per run, than it needs to.

## `INSUFFICIENT_ACCESS_OR_READONLY`

The Salesforce user the connection is authenticated as doesn't have permission to perform the specific operation (create/edit/delete on that object, or on that specific field). This is a Salesforce-side profile/permission-set issue, not something to fix in Umbraco — grant the connected user's Salesforce profile the needed object/field-level permissions.

## `REQUIRED_FIELD_MISSING`

The action's field map is missing a field Salesforce requires for that object (e.g. Lead requires `LastName` and `Company`). The error message names which field(s). For Create Lead specifically, `Last Name` and `Company` are already required in the action's own configuration; for Create Record/Update Record/Upsert Record, check the object's page layout or Object Manager in Salesforce Setup for what else it requires.

## `DUPLICATE_VALUE`

Salesforce rejected the write because of a duplicate rule or a unique-field constraint. If you're deliberately trying to avoid duplicates on repeated runs, use Upsert Record with an External ID field instead of Create Record — see [docs/actions.md](actions.md).

## "A common grant type/response type combination... couldn't be negotiated automatically" when clicking Authenticate with Salesforce

This is an OAuth client configuration problem, not a per-connection issue — every connection attempt will fail the same way until it's fixed. It means the Salesforce OAuth client registration's allowed grant types don't include `authorization_code`. This should not happen with a stock install of this package; if you're seeing it after modifying the package's own OAuth composer code, the client-wide grant-type allow-list needs to explicitly include both `authorization_code` and `refresh_token` — omitting one silently breaks the other flow.

## An automation is stuck in "Running" forever, with no step ever executing

This is very unlikely to be a Salesforce-specific problem, even though it surfaces on a Salesforce automation. It's almost always Umbraco Automate's outbox dispatcher never becoming eligible to process on a single-server/self-hosted install, because the server role couldn't be determined. Check the server log for a line like:

```
Outbox has N pending message(s) but no topics are eligible for processing on this node... ServerRole is Unknown
```

Fix by setting, in your hosting configuration:

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

## Site fails to start with "Umbraco Automate requires a database connection string named 'umbracoAutomateDbDSN'"

This is an Umbraco Automate (core) configuration requirement, not something this package adds — Automate needs its own connection string, separate from (or, via `UseNamedConnectionString`, shared with) the main Umbraco CMS database. Add an `umbracoAutomateDbDSN` entry under `ConnectionStrings`, or set `Umbraco:Automate:UseNamedConnectionString` to the name of an existing connection string (e.g. `umbracoDbDSN`) to reuse it. See Umbraco Automate's own installation documentation.

## Opportunity Stage Changed never fires

- Confirm the automation has actually been published, not just saved as a draft — a polling trigger only starts checking once its automation is published.
- Remember the very first poll after publishing only establishes a baseline; it will not fire for a stage change that already happened before that first poll.
- Confirm the workspace the automation lives in has a Salesforce connection available to it, and that connection's "Test connection" succeeds.
- Check the server log for a warning from the trigger naming the failure reason — a typo'd Target Stage value won't error, it will just never match, which looks identical to "nothing happened" from the outside.

## Live-org integration tests are skipped / no-op in CI

This is expected and correct — the opt-in live-organization integration tests (`tests/Umbraco.Automate.Salesforce.Tests.Integration`) only run when a local `.env` file with real Connected App credentials is present. They silently no-op otherwise so CI and other developers' machines are never affected by their absence. See that test project's `LiveSalesforceCredentials` for the exact file it looks for.
