using System.Xml.Linq;
using Umbraco.Automate.Core.Actions;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Parses a Salesforce Outbound Message's raw SOAP XML body into typed fields.
/// </summary>
/// <remarks>
/// <para>
/// This is an action, not a trigger, even though §6 of the brief describes "Webhook / Outbound
/// Message Received" as a trigger. Confirmed by reading <c>WebhookEndpointController</c>: the
/// inbound webhook endpoint is hardcoded to Core's own concrete <c>WebhookTrigger</c> type — it
/// resolves <c>_triggers.GetByAlias&lt;WebhookTrigger&gt;(...)</c> specifically, not any
/// <c>IWebhookTrigger</c>. There is no extensibility point for a third-party trigger to receive
/// webhook dispatch at all in this platform version. The only way to receive Salesforce's
/// Outbound Message today is: point it at an automation using Core's own built-in Webhook
/// trigger, then use this action (bound to <c>${trigger.body}</c>) to parse the SOAP XML that
/// arrives as a raw string, since Core's <c>WebhookTriggerOutput</c> has no SOAP-awareness of
/// its own. Needs no Salesforce connection or live org — it's pure XML parsing.
/// </para>
/// <para>
/// Requires no <c>chatter_api</c>-style OAuth scope. This is why it has no
/// <c>ConnectionTypeAlias</c> — it's fine, though unusual, for an action to not need a connection.
/// </para>
/// </remarks>
[Action("salesforce.parseOutboundMessage", "Parse Outbound Message",
    Description = "Parses a Salesforce Outbound Message's raw SOAP XML body (received via Core's Webhook trigger) into typed fields.",
    Group = "CRM",
    Icon = "icon-code")]
public sealed class ParseOutboundMessageAction : ActionBase<ParseOutboundMessageSettings, ParseOutboundMessageOutput>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParseOutboundMessageAction"/> class.
    /// </summary>
    public ParseOutboundMessageAction(ActionInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<ParseOutboundMessageSettings>();

        if (string.IsNullOrWhiteSpace(settings.RawXml))
        {
            return Task.FromResult(ActionResult.Failed(
                new ArgumentException("Raw XML is required."), StepRunErrorCategory.Validation));
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(settings.RawXml);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException)
        {
            return Task.FromResult(Success(new ParseOutboundMessageOutput { Parsed = false }));
        }

        // Match by local name, ignoring namespace prefixes — Salesforce's own examples vary them.
        var notifications = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "notifications");
        var notification = notifications?.Elements().FirstOrDefault(e => e.Name.LocalName == "Notification");
        var sObject = notification?.Elements().FirstOrDefault(e => e.Name.LocalName == "sObject");

        if (notifications is null || sObject is null)
        {
            return Task.FromResult(Success(new ParseOutboundMessageOutput { Parsed = false }));
        }

        var organizationId = notifications.Elements().FirstOrDefault(e => e.Name.LocalName == "OrganizationId")?.Value;

        var xsiType = sObject.Attributes().FirstOrDefault(a => a.Name.LocalName == "type")?.Value;
        // Strip a namespace prefix like "sf:" if present.
        var objectType = xsiType is { } t && t.Contains(':') ? t[(t.IndexOf(':') + 1)..] : xsiType;

        var fields = new Dictionary<string, object?>();
        string? recordId = null;
        foreach (var field in sObject.Elements())
        {
            fields[field.Name.LocalName] = field.Value;
            if (field.Name.LocalName == "Id")
            {
                recordId = field.Value;
            }
        }

        return Task.FromResult(Success(new ParseOutboundMessageOutput
        {
            Parsed = true,
            OrganizationId = organizationId,
            ObjectType = objectType,
            RecordId = recordId,
            Fields = fields,
        }));
    }
}
