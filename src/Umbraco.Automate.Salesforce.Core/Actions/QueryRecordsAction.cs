using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Runs a bounded SOQL query and returns matching records.
/// Requires a Salesforce connection with the <c>api</c> scope.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bounding:</b> every query gets a <c>LIMIT</c> clause, capped server-side by
/// <c>Umbraco:Automate:Salesforce:MaxQueryRows</c> regardless of what the query or the step's
/// "Max Rows" setting ask for (CLAUDE.md §7: "block obviously unsafe/unbounded queries").
/// </para>
/// <para>
/// <b>Injection:</b> this rejects anything that isn't a <c>SELECT</c> and any semicolon (defense
/// in depth — Salesforce's query endpoint only ever runs one SELECT regardless). It does
/// <em>not</em> guarantee bound values embedded in the query text are escaped: Core's <c>${ }</c>
/// binding substitution runs before <c>ExecuteAsync</c> is called, so by the time this action
/// sees the query string, any bound value has already been spliced in — this action has no
/// opportunity to escape a value that arrived pre-substituted inside a string literal. Automation
/// authors who splice a bound value into a <c>WHERE ... = '...'</c> clause themselves should wrap
/// it with <see cref="Api.SalesforceSoqlEscaper.EscapeStringLiteral"/> in the binding expression,
/// or use a value that's controlled (e.g. an ID from a prior step), not raw untrusted end-user
/// text (e.g. a webhook payload) — see CLAUDE.md §0a for the full reasoning.
/// </para>
/// </remarks>
[Action("salesforce.queryRecords", "Query Records (SOQL)",
    Description = "Runs a bounded SOQL query and returns matching records.",
    Group = "CRM",
    Icon = "icon-search",
    ConnectionTypeAlias = "salesforce")]
public sealed class QueryRecordsAction : ActionBase<QueryRecordsSettings, QueryRecordsOutput>
{
    private static readonly Regex LimitClauseRegex = new(
        @"\bLIMIT\s+(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ISalesforceClient _client;
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRecordsAction"/> class.
    /// </summary>
    public QueryRecordsAction(
        ActionInfrastructure infrastructure,
        ISalesforceConnectionResolver connectionResolver,
        ISalesforceClient client,
        IOptionsMonitor<SalesforceApiOptions> options)
        : base(infrastructure)
    {
        _connectionResolver = connectionResolver;
        _client = client;
        _options = options;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<QueryRecordsSettings>();
        var soql = settings.Soql?.Trim() ?? string.Empty;

        if (soql.Length == 0)
        {
            return ActionResult.Failed(new ArgumentException("SOQL Query is required."), StepRunErrorCategory.Validation);
        }

        if (!soql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return ActionResult.Failed(
                new ArgumentException("SOQL Query must be a SELECT statement."), StepRunErrorCategory.Validation);
        }

        if (soql.Contains(';'))
        {
            return ActionResult.Failed(
                new ArgumentException("SOQL Query must not contain a semicolon."), StepRunErrorCategory.Validation);
        }

        if (SalesforceActionSupport.TryGetCredentialsId(context.Connection, out var credentialsId) is { } credentialFailure)
        {
            return credentialFailure;
        }

        var (connection, connectionFailure) = await SalesforceActionSupport.ResolveContextAsync(
            credentialsId, _connectionResolver, cancellationToken);
        if (connectionFailure is not null)
        {
            return connectionFailure;
        }

        var serverCap = _options.CurrentValue.MaxQueryRows;
        var requested = settings.MaxRows > 0 ? settings.MaxRows : serverCap;
        var effectiveMaxRows = Math.Min(requested, serverCap);

        var boundedSoql = ApplyRowLimit(soql, effectiveMaxRows);

        var apiVersion = _options.CurrentValue.ApiVersion;
        var result = await _client.SendAsync(
            connection!,
            HttpMethod.Get,
            $"/services/data/{apiVersion}/query?q={Uri.EscapeDataString(boundedSoql)}",
            jsonBody: null,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return SalesforceActionSupport.Failed(result);
        }

        var json = result.Json!.Value;
        var totalSize = json.TryGetProperty("totalSize", out var t) ? t.GetInt32() : 0;
        var records = new List<IReadOnlyDictionary<string, object?>>();
        if (json.TryGetProperty("records", out var recordsElement))
        {
            foreach (var record in recordsElement.EnumerateArray())
            {
                records.Add(SalesforceJsonHelpers.ToFieldDictionary(record));
            }
        }

        return Success(new QueryRecordsOutput
        {
            Records = records,
            TotalSize = totalSize,
            // Heuristic, not exact: a LIMIT clause caps how many rows Salesforce returns, so
            // hitting the cap exactly is the only signal available (without an extra COUNT()
            // query) that more rows may have matched than were returned.
            Truncated = records.Count >= effectiveMaxRows,
        });
    }

    /// <summary>
    /// Caps an existing <c>LIMIT</c> clause to <paramref name="maxRows"/>, or appends one if the
    /// query doesn't have one. Internal (not private) so <c>Tests.Unit</c> can exercise the
    /// bounding logic directly without going through the full action pipeline.
    /// </summary>
    internal static string ApplyRowLimit(string soql, int maxRows)
    {
        var match = LimitClauseRegex.Match(soql);
        if (!match.Success)
        {
            return $"{soql} LIMIT {maxRows}";
        }

        var existingLimit = int.Parse(match.Groups[1].Value);
        if (existingLimit <= maxRows)
        {
            return soql;
        }

        return LimitClauseRegex.Replace(soql, $"LIMIT {maxRows}");
    }
}
