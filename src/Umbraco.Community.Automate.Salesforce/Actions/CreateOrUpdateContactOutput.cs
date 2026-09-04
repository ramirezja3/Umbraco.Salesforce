namespace Umbraco.Community.Automate.Salesforce.Actions;

/// <summary>
/// Output produced by the <see cref="CreateOrUpdateContactAction"/>.
/// </summary>
public sealed class CreateOrUpdateContactOutput
{
    /// <summary>
    /// Gets the Id of the created or updated Contact.
    /// </summary>
    public string? RecordId { get; init; }

    /// <summary>
    /// Gets a value indicating whether a new Contact was created (<c>true</c>) or an existing
    /// one was updated (<c>false</c>).
    /// </summary>
    public bool Created { get; init; }
}
