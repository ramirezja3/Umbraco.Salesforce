using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Settings for the <see cref="ParseOutboundMessageAction"/>.
/// </summary>
public sealed class ParseOutboundMessageSettings
{
    /// <summary>
    /// Gets or sets the raw SOAP XML body of a Salesforce Outbound Message. Typically bound to
    /// <c>${trigger.body}</c> from Core's built-in Webhook trigger — see
    /// <see cref="ParseOutboundMessageAction"/>'s remarks for why this is an action rather than
    /// its own trigger type.
    /// </summary>
    [Field(Label = "Raw XML", Description = "The raw SOAP XML body received from Salesforce.",
        SupportsBindings = true, EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
    public string RawXml { get; set; } = string.Empty;
}
