using System.Text.Json.Nodes;

namespace Umbraco.Community.Automate.Salesforce.Tests.Integration.LiveSalesforce;

/// <summary>
/// Loads Salesforce Connected App credentials for the opt-in live-org integration tests in this
/// folder, from a developer's local, gitignored <c>.env</c> file (never committed — see the
/// repo's <c>.gitignore</c>) or the <c>SALESFORCE_TEST_ENV_PATH</c> environment variable pointing
/// at one. Returns <c>null</c> when neither is present, so CI and other developers' machines
/// skip these tests instead of failing — there is no credential anywhere in source control.
/// </summary>
/// <remarks>
/// Expected file shape (a bare JSON fragment, matching how it was handed to this package for
/// local testing):
/// <code>
/// "Salesforce": {
///   "MyDomainUrl": "https://your-org.my.salesforce.com",
///   "ClientId": "...",
///   "ClientSecret": "...",
///   "ApiVersion": "v61.0"
/// }
/// </code>
/// </remarks>
public sealed record LiveSalesforceCredentials(string MyDomainUrl, string ClientId, string ClientSecret, string ApiVersion)
{
    private const string EnvPathVariable = "SALESFORCE_TEST_ENV_PATH";

    public static LiveSalesforceCredentials? TryLoad()
    {
        var path = Environment.GetEnvironmentVariable(EnvPathVariable) ?? FindEnvFileNearby();
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var raw = File.ReadAllText(path);
            var root = JsonNode.Parse("{" + raw + "}");
            var cfg = root?["Salesforce"];
            if (cfg is null)
            {
                return null;
            }

            return new LiveSalesforceCredentials(
                cfg["MyDomainUrl"]!.GetValue<string>().TrimEnd('/'),
                cfg["ClientId"]!.GetValue<string>(),
                cfg["ClientSecret"]!.GetValue<string>(),
                cfg["ApiVersion"]!.GetValue<string>());
        }
        catch
        {
            // Malformed/partial file — treat as "not configured" rather than failing every test.
            return null;
        }
    }

    /// <summary>Walks up from the test binary's output directory looking for a <c>.env</c> at the repo root.</summary>
    private static string? FindEnvFileNearby()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }
}
