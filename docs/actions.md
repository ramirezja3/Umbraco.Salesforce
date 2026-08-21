# Actions

Every action below requires only the `api` and `refresh_token` scopes granted during installation (see [docs/installation.md](installation.md)) — no action in this package needs an additional Salesforce OAuth scope.

Every input field can be a literal value or a `${ }` binding to an earlier trigger's or step's output (e.g. `${ trigger.Email }`) — see Umbraco Automate's own binding documentation for the full syntax. Every write action returns the affected record's Salesforce Id and a success/failure status usable by later steps.

## Create Lead

The most common action in this package — creates a Salesforce Lead from named fields, for the classic "someone did something on the website, get them into the CRM as a Lead" flow.

**Inputs:**

| Field | Required | Notes |
|---|---|---|
| Last Name | Yes | |
| Company | Yes | Required by Salesforce unless the organization has Person Accounts enabled |
| First Name | No | |
| Email | No | |
| Phone | No | |
| Title | No | |
| Lead Source | No | Must match a configured picklist value in the organization, e.g. "Web" |
| Status | No | Leave empty to use the organization's default Lead status |
| Additional Fields | No | A JSON object for anything not covered above, e.g. `{"Industry": "Technology"}`. Named fields above always win if both set the same key. |

**Output:** the new Lead's Salesforce record Id.

**Example:** a Member Saved trigger firing on member registration → Create Lead, with Email bound to `${ trigger.Email }` and Last Name bound to `${ trigger.MemberName }`.

## Create Record

The general-purpose write action for any Salesforce object *other* than Lead (Contact, Account, Opportunity, Case, custom objects) — use Create Lead instead for Leads specifically.

**Inputs:**
- **Object** — the Salesforce object API name, e.g. `Contact`, `Case`, `My_Custom_Object__c`.
- **Fields** — a JSON object of field values, e.g. `{"LastName": "Smith", "AccountId": "001xx..."}`.

**Output:** the new record's Salesforce Id.

## Update Record

Updates an existing record by Id.

**Inputs:**
- **Object** — the Salesforce object API name.
- **Record Id** — the Id of the record to update.
- **Fields** — a JSON object of the field values to change, e.g. `{"Status": "Closed"}`.

## Upsert Record

Creates the record if it doesn't already exist, or updates it if it does, matched by an **External ID** field rather than the Salesforce record Id. This is the retry-safe, idempotent write path — prefer it over Create Record whenever an automation might run more than once for the same real-world entity (the most common case: a repeated form submission from the same person shouldn't create a duplicate Contact/Lead).

**Inputs:**
- **Object** — the Salesforce object API name.
- **External Id Field** — the API name of the External ID field to match on, e.g. `Website_User_Id__c`.
- **External Id Value** — the value to match against that field.
- **Fields** — a JSON object of the field values to set.

**Example:** a form submission automation upserting a Contact keyed by email, so resubmitting the same form doesn't create a second Contact.

## Get Record

Retrieves a single record by Id for use by later steps in the same automation (e.g. to personalize a page, or to check a field's current value before deciding what to do next).

**Inputs:**
- **Object** — the Salesforce object API name.
- **Record Id** — the Id of the record to retrieve.
- **Fields** — comma-separated field API names to return, e.g. `Id,Name,Email`. Leave empty to return all fields.

## Delete Record

Deletes a record by Id. **Destructive and cannot be undone.**

**Inputs:**
- **Object** — the Salesforce object API name.
- **Record Id** — the Id of the record to delete.
- **Confirm Delete** — must be explicitly checked, or the step fails validation. This exists specifically to stop an accidental destructive automation from shipping unnoticed.

**Example:** a member requests account deletion on the site (a GDPR/data-erasure request) → Delete Record (or Update Record, to anonymize instead of delete) on their matching Salesforce Contact/Lead.

## Query Records (SOQL)

Runs a bounded SOQL `SELECT` query and returns the matching records — the lookup/dedupe-check action (e.g. "does a Lead with this email already exist" before deciding whether to create one).

**Inputs:**
- **SOQL Query** — a `SELECT` statement. Must start with `SELECT`, may not contain a semicolon, and its row count is capped (see Max Rows) regardless of what `LIMIT` the query itself specifies.
- **Max Rows** — the maximum number of rows to return (default 200).

**Security note:** if you build a `WHERE ... = '${ binding }'` clause yourself using a bound value, escape it — Umbraco Automate's binding substitution happens before this action ever sees the settings, so it has no way to escape a value on your behalf. This package's SOQL escaping helper exists for exactly this case; do not string-concatenate untrusted binding values directly into a query.

**Example:** before running Create Lead on a form submission, run Query Records with `SELECT Id FROM Lead WHERE Email = '...'` and branch to Update Record instead if a match is found — or just use Upsert Record, which does this in one step when an External ID field is available.
