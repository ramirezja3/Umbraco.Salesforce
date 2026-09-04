using Microsoft.Extensions.Configuration;
using Umbraco.Automate.Salesforce.Configuration;

namespace Umbraco.Automate.Salesforce.Tests.Unit;

public class SalesforceComposerTests
{
    private static IConfiguration ConfigWith(params KeyValuePair<string, string?>[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void ResolveScopes_NoScopesConfigured_FallsBackToApiAndRefreshToken()
    {
        var config = ConfigWith();

        var scopes = SalesforceComposer.ResolveScopes(config, "Salesforce");

        scopes.ShouldBe(["api", "refresh_token"]);
    }

    [Fact]
    public void ResolveScopes_ScopesConfigured_UsesConfiguredValuesInstead()
    {
        // Umbraco:Automate:Providers:Salesforce:Scopes must actually take effect on the OAuth
        // request — an implementer adding e.g. "chatter_api" here should see it requested.
        var config = ConfigWith(
            new("Umbraco:Automate:Providers:Salesforce:Scopes:0", "api"),
            new("Umbraco:Automate:Providers:Salesforce:Scopes:1", "refresh_token"),
            new("Umbraco:Automate:Providers:Salesforce:Scopes:2", "chatter_api"));

        var scopes = SalesforceComposer.ResolveScopes(config, "Salesforce");

        scopes.ShouldBe(["api", "refresh_token", "chatter_api"]);
    }

    [Fact]
    public void ResolveScopes_EmptyScopesArrayConfigured_FallsBackToDefault()
    {
        var config = ConfigWith(new KeyValuePair<string, string?>("Umbraco:Automate:Providers:Salesforce:Scopes", ""));

        var scopes = SalesforceComposer.ResolveScopes(config, "Salesforce");

        scopes.ShouldBe(["api", "refresh_token"]);
    }

    [Fact]
    public void ResolveScopes_ReadsPerProviderName_OtherProviderDoesNotSeeSalesforceScopes()
    {
        var config = ConfigWith(
            new("Umbraco:Automate:Providers:Salesforce:Scopes:0", "api"),
            new("Umbraco:Automate:Providers:Salesforce:Scopes:1", "refresh_token"),
            new("Umbraco:Automate:Providers:Salesforce:Scopes:2", "chatter_api"));

        var otherScopes = SalesforceComposer.ResolveScopes(config, "SomeOtherProvider");

        otherScopes.ShouldBe(["api", "refresh_token"]);
    }
}
