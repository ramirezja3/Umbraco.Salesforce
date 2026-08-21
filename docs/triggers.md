# Triggers

This package ships one Salesforce-side trigger. That's deliberate, not incomplete: for a CMS-to-CRM integration, Umbraco is almost always the side *initiating* events into Salesforce (a form submission, a member signing up), which is what the seven actions in [docs/actions.md](actions.md) are for. Opportunity Stage Changed is the one pattern that genuinely runs the other direction with clear value to a website: a deal closing in Salesforce driving something on the site.

## Opportunity Stage Changed

Fires when an Opportunity's `StageName` changes, optionally filtered to a specific target stage (e.g. only fire on "Closed Won").

**Configuration:**
- **Target Stage** — optional. Leave empty to fire on *any* stage change; set it (e.g. `Closed Won`) to only fire when the Opportunity moves specifically to that stage.

**Outputs:**

| Field | Description |
|---|---|
| Opportunity Id | The changed Opportunity's Salesforce Id |
| Previous Stage | The stage it moved from |
| New Stage | The stage it moved to |
| Amount | The Opportunity's Amount field |
| Account Id | The associated Account's Salesforce Id |
| Owner Id | The Opportunity owner's Salesforce user Id |

**Example:** target stage "Closed Won" → Publish Content (Umbraco Automate's own core action) to add a case study to a public list, or notify the account owner.

## How it fires

This trigger polls Salesforce rather than subscribing to a live event stream (Salesforce's Change Data Capture / Pub-Sub API would give near-real-time firing, but isn't something this package can build or test without a live, CDC-enabled organization to develop against — polling is the documented, working fallback). It checks on the interval configured by:

```json
{
  "Umbraco": {
    "Automate": {
      "Salesforce": {
        "Polling": {
          "PollInterval": "00:01:00"
        }
      }
    }
  }
}
```

The first poll after an automation using this trigger is published only establishes a baseline — it does not fire retroactively for stage changes that already happened before the automation existed.

**Connection:** unlike an action's per-step connection picker, this trigger has no dedicated connection setting — it uses whichever Salesforce connection is available to the automation's workspace. If a workspace has more than one Salesforce connection, the first one found is used (and a warning is logged) — scope workspaces to a single Salesforce connection if you need to avoid ambiguity.
