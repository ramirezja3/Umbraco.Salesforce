# Actions

Every action below requires only the `api` and `refresh_token` scopes granted during installation (see [docs/installation.md](installation.md)) — no action in this package needs an additional Salesforce OAuth scope.

Every input field can be a literal value or a `${ }` binding to an earlier trigger's or step's output (e.g. `${ trigger.Email }`) — see Umbraco Automate's own binding documentation for the full syntax. Every write action returns the affected record's Salesforce Id and a success/failure status usable by later steps.

Each action targets one specific, named Salesforce object with named fields — there's no "pick an object API name" action in this package. If you need to write to a Salesforce object not covered here, that's currently outside this package's scope.

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

## Create/Update Contact

Creates a new Salesforce Contact, or updates one by Id — the "known customer" counterpart to Create Lead. Switching between create and update is explicit (via **Contact Id**), not an automatic email lookup: the intended flow is to create a Contact on a customer's first web action and store the returned record Id somewhere (e.g. on an Umbraco member), then bind that Id back in on later automations to update the same Contact.

**Inputs:**

| Field | Required | Notes |
|---|---|---|
| Last Name | Yes | |
| First Name | No | |
| Email | No | |
| Phone | No | |
| Account Id | No | The Salesforce Id of the Account (company) this Contact belongs to. Not required by Salesforce itself, but many organizations require it via their own validation rules. |
| Contact Id (to update) | No | Leave empty to create a new Contact. Set to an existing Contact's Id to update it instead. |
| Additional Fields | No | A JSON object for anything not covered above, e.g. `{"Department": "Engineering"}`. |

**Output:** the created/updated Contact's Salesforce Id, and whether a new Contact was created.

**Example:** a customer creates an account on a Commerce site → Create/Update Contact with no Contact Id (creates); store the returned Id on the member. Later, when their profile changes → Create/Update Contact again with that stored Id (updates the same Contact).

## Create Opportunity

Creates a Salesforce Opportunity — logs a deal, e.g. when a Commerce order is placed.

**Inputs:**

| Field | Required | Notes |
|---|---|---|
| Name | Yes | A name for the opportunity, e.g. the order reference |
| Stage | Yes | Must match a configured Opportunity Stage picklist value, e.g. "Prospecting" |
| Close Date | Yes | As `YYYY-MM-DD` |
| Account Id | No | The associated Account's Salesforce Id. Not required by Salesforce itself, but many organizations require it via their own validation rules. |
| Amount | No | |
| Additional Fields | No | A JSON object for anything not covered above, e.g. `{"LeadSource": "Website"}`. |

**Output:** the new Opportunity's Salesforce record Id.

**Example:** a Commerce order is placed → Create Opportunity with Name bound to the order reference, Amount bound to the order total.

## Update Opportunity Stage

Moves an existing Opportunity to a new stage — pairs naturally with Create Opportunity, progressing the same deal as its real-world status changes.

**Inputs:**

| Field | Required | Notes |
|---|---|---|
| Opportunity Id | Yes | |
| Stage | Yes | Must match a configured Opportunity Stage picklist value, e.g. "Closed Won" |

**Output:** the Opportunity's Salesforce Id (echoed back, for convenience binding into later steps).

**Example:** a Commerce order ships → Update Opportunity Stage to "Closed Won"; an order is refunded or cancelled → "Closed Lost".

## Add to Campaign

Adds a Contact or Lead to a Salesforce Campaign — the "this person did the marketing thing" action.

**Inputs:**

| Field | Required | Notes |
|---|---|---|
| Campaign Id | Yes | |
| Contact Id | One of Contact Id / Lead Id | Provide exactly one, not both |
| Lead Id | One of Contact Id / Lead Id | Provide exactly one, not both |
| Status | No | Campaign member status, e.g. "Responded". Defined per-campaign in Salesforce Setup, not a global picklist — leave empty to use the campaign's own configured default status |

**Output:** the new CampaignMember record's Salesforce Id.

**Example:** an Engage-personalized journey completes, or a visitor downloads gated content → Add to Campaign with the matching Contact/Lead Id.

## Log Engagement Activity

Logs a completed activity (a Salesforce Task) against a Contact or Lead — turns an on-site behavioral signal into something a salesperson sees directly on that person's Activity History timeline in Salesforce.

**Inputs:**

| Field | Required | Notes |
|---|---|---|
| Contact or Lead Id | Yes | Salesforce's `Task.WhoId` accepts either type directly |
| Subject | Yes | A short summary, e.g. "Website Engagement: Downloaded Pricing Guide" |
| Description | No | |
| Activity Date | No | As `YYYY-MM-DD`. Leave empty to leave the date unset |

The logged Task is always marked "Completed" — this action logs something that already happened, not an assigned to-do, so there's no status to pick.

**Output:** the new Task's Salesforce Id.

**Example:** an Engage segment match (e.g. "re-engaged after 30 days inactive") → Log Engagement Activity against that visitor's Contact/Lead Id, so the sales team sees it on their timeline.
