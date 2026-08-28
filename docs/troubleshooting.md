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

Salesforce requires a field beyond what this action's named settings cover — usually a custom required field or validation rule your org added (e.g. an org that requires `AccountId` on Opportunity or Contact via a validation rule, even though Salesforce's platform doesn't require it by default). The error message names which field(s). Add the missing value via the action's **Additional Fields** JSON input where available (Create Lead, Create/Update Contact, Create Opportunity); actions without an Additional Fields input (Update Opportunity Stage, Add to Campaign, Log Engagement Activity) can't supply extra fields — check Object Manager in Salesforce Setup for what else that object requires.

## `DUPLICATE_VALUE`

Salesforce rejected the write because of a duplicate rule or a unique-field constraint (e.g. re-running **Create/Update Contact** with no Contact Id creates a second Contact for the same person). If an automation might run more than once for the same real-world entity, store the record Id returned by the first run (e.g. on an Umbraco member) and pass it back in on later runs so the action updates instead of creating again — see [docs/actions.md](actions.md).

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

## Live-org integration tests are skipped / no-op in CI

This is expected and correct — the opt-in live-organization integration tests (`tests/Umbraco.Automate.Salesforce.Tests.Integration`) only run when a local `.env` file with real Connected App credentials is present. They silently no-op otherwise so CI and other developers' machines are never affected by their absence. See that test project's `LiveSalesforceCredentials` for the exact file it looks for.
