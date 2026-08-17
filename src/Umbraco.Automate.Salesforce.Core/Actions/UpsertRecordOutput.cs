namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Output produced by the <see cref="UpsertRecordAction"/>.
/// </summary>
public sealed class UpsertRecordOutput
{
    /// <summary>
    /// Gets the ID of the created or updated record. Salesforce only returns the ID when a new
    /// record was created (HTTP 201) — when an existing record was matched and updated instead
    /// (HTTP 204), Salesforce returns no body at all, so this is <c>null</c> in that case; use
    /// <see cref="Created"/> to tell the two cases apart.
    /// </summary>
    public string? RecordId { get; init; }

    /// <summary>
    /// Gets a value indicating whether a new record was created (<c>true</c>) or an existing
    /// record matched by the external ID was updated instead (<c>false</c>).
    /// </summary>
    public required bool Created { get; init; }
}
