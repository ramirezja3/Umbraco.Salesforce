using Umbraco.Automate.Core.Actions;

namespace Umbraco.Community.Automate.Salesforce.Api;

/// <summary>
/// A mapped, human-readable Salesforce API error — see <see cref="SalesforceErrorMapper"/>.
/// </summary>
public sealed record SalesforceApiError(string Message, string? ErrorCode, StepRunErrorCategory Category);
