namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Output produced by the <see cref="ParseOutboundMessageAction"/>.
/// </summary>
public sealed class ParseOutboundMessageOutput
{
    /// <summary>Gets a value indicating whether the XML was successfully parsed as an Outbound Message.</summary>
    public required bool Parsed { get; init; }

    /// <summary>Gets the Salesforce organization ID that sent the message.</summary>
    public string? OrganizationId { get; init; }

    /// <summary>Gets the sObject's type (e.g. "Lead", "Case"), with any namespace prefix stripped.</summary>
    public string? ObjectType { get; init; }

    /// <summary>Gets the record's ID.</summary>
    public string? RecordId { get; init; }

    /// <summary>Gets the record's field values, keyed by field API name.</summary>
    public IReadOnlyDictionary<string, object?> Fields { get; init; } = new Dictionary<string, object?>();
}
