namespace Automate.Salesforce.Connector.Api;

/// <summary>
/// A Salesforce REST API call failed. Carries the mapped human-readable message (see
/// <see cref="SalesforceErrorMapper"/>) rather than a raw JSON error dump, so it reads cleanly
/// in the automation Run log.
/// </summary>
public sealed class SalesforceApiException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SalesforceApiException"/> class.
    /// </summary>
    public SalesforceApiException(string message, string? errorCode = null) : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the Salesforce error code (e.g. <c>INVALID_FIELD</c>, <c>REQUEST_LIMIT_EXCEEDED</c>),
    /// or <c>null</c> if the failure wasn't a structured Salesforce API error.
    /// </summary>
    public string? ErrorCode { get; }
}
