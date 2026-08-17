using System.Globalization;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Small shared helpers for polling triggers.
/// </summary>
internal static class SalesforceTriggerSupport
{
    /// <summary>
    /// Formats a UTC <see cref="DateTime"/> as a SOQL datetime literal (unquoted, unlike a SOQL
    /// string literal).
    /// </summary>
    public static string FormatSoqlDateTime(DateTime utc)
        => utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
}
