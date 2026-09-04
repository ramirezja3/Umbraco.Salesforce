using System.Text.Json;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Shared checks every Salesforce action performs before calling the API: resolving the
/// configured connection's credential ID, resolving a live access token + instance URL for it,
/// and parsing a JSON field-map setting. Factored out because every action in this package
/// repeats the same three steps — mirrors the shape of <c>Umbraco.Automate.Slack</c>'s
/// <c>SendMessageAction</c> checks, just shared across more than one action.
/// </summary>
internal static class SalesforceActionSupport
{
    /// <summary>
    /// Reads the OAuth credential ID off the connection's settings. Returns a ready-made
    /// failure result if there's no connection, or it isn't a Salesforce connection type, or it
    /// hasn't been authenticated yet.
    /// </summary>
    public static ActionResult? TryGetCredentialsId(ConfiguredConnection? connection, out Guid credentialsId)
    {
        credentialsId = Guid.Empty;

        if (connection is null)
        {
            return ActionResult.Failed(
                new InvalidOperationException("A Salesforce connection is required for this step."),
                StepRunErrorCategory.Validation);
        }

        if (connection.Settings is not SalesforceConnectionSettings settings
            || settings.OAuthCredentialsId is not { } id
            || id == Guid.Empty)
        {
            return ActionResult.Failed(
                new InvalidOperationException("This connection has not been authenticated with Salesforce yet."),
                StepRunErrorCategory.Validation);
        }

        credentialsId = id;
        return null;
    }

    /// <summary>
    /// Resolves a valid access token and instance URL for the given credential. Returns a
    /// ready-made failure result if the token is expired/revoked and can't be refreshed.
    /// </summary>
    public static async Task<(SalesforceConnectionContext? Context, ActionResult? Failure)> ResolveContextAsync(
        Guid credentialsId, ISalesforceConnectionResolver resolver, CancellationToken cancellationToken)
    {
        var connectionContext = await resolver.ResolveAsync(credentialsId, cancellationToken);
        if (connectionContext is null)
        {
            return (null, ActionResult.Failed(
                new InvalidOperationException("The Salesforce access token is expired or revoked. Please re-authenticate the connection."),
                StepRunErrorCategory.Authentication));
        }

        return (connectionContext, null);
    }

    /// <summary>
    /// Parses a JSON object field-map setting (e.g. <c>CreateLeadSettings.AdditionalFields</c>)
    /// into a dictionary. Returns a ready-made validation failure if it isn't valid JSON.
    /// </summary>
    /// <remarks>
    /// The returned dictionary uses <see cref="StringComparer.OrdinalIgnoreCase"/>, not the
    /// default ordinal comparer <see cref="JsonSerializer"/> would otherwise produce. Callers like
    /// <c>CreateLeadAction</c> parse an <c>AdditionalFields</c> JSON blob into this dictionary and
    /// then assign named fields on top
    /// of it (e.g. <c>fields["Email"] = settings.Email;</c>) so the named field always wins over a
    /// duplicate in <c>AdditionalFields</c>. Salesforce field API names are themselves
    /// case-insensitive, so a differently-cased duplicate (e.g. <c>"email"</c> in
    /// <c>AdditionalFields</c>) must be recognized as the same key or both end up in the outgoing
    /// JSON body instead of the named field cleanly winning. Built via explicit indexer
    /// assignment (not a dictionary constructor/bulk-add) so a JSON payload that itself contains
    /// case-variant duplicate keys degrades to "last one wins" rather than throwing.
    /// </remarks>
    public static ActionResult? TryParseFields(string? json, out Dictionary<string, object?> fields)
    {
        fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? [];
            foreach (var (key, value) in parsed)
            {
                fields[key] = value;
            }

            return null;
        }
        catch (JsonException)
        {
            // Names the expected shape and echoes the offending input instead of surfacing the
            // raw System.Text.Json parser message, which is meaningless to an implementer who
            // doesn't know the internal parser's error format.
            var preview = json.Length > 80 ? json[..80] + "…" : json;
            return ActionResult.Failed(
                new ArgumentException(
                    $$"""Fields must be a JSON object of field API names to values, e.g. {"Description": "..."} — got: {{preview}}"""),
                StepRunErrorCategory.Validation);
        }
    }

    /// <summary>
    /// Maps a failed <see cref="SalesforceApiResult"/> into an <see cref="ActionResult"/>,
    /// using the already-mapped human-readable error (see <see cref="SalesforceErrorMapper"/>).
    /// </summary>
    public static ActionResult Failed(SalesforceApiResult result)
        => ActionResult.Failed(
            new SalesforceApiException(result.Error!.Message, result.Error.ErrorCode),
            result.Error.Category);
}
