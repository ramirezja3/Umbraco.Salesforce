using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Salesforce.Actions;

namespace Umbraco.Automate.Salesforce.Tests.Unit;

public class ParseOutboundMessageActionTests
{
    private const string SampleOutboundMessage = """
        <?xml version="1.0" encoding="UTF-8"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns="http://soap.sforce.com/2005/09/outbound">
          <soapenv:Body>
            <notifications>
              <OrganizationId>00Dxx0000000001</OrganizationId>
              <ActionId>04kxx0000000001</ActionId>
              <Notification>
                <Id>04lxx0000000001</Id>
                <sObject xsi:type="sf:Lead" xmlns:sf="urn:sobject.enterprise.soap.sforce.com" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <sf:Id>00Qxx0000000001</sf:Id>
                  <sf:LastName>Smith</sf:LastName>
                  <sf:Company>Acme</sf:Company>
                </sObject>
              </Notification>
            </notifications>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

    private static ParseOutboundMessageAction CreateAction()
        => new(new ActionInfrastructure(Mock.Of<IEditableModelResolver>()));

    private static ActionContext ContextWith(string rawXml) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "salesforce.parseOutboundMessage",
        Settings = new ParseOutboundMessageSettings { RawXml = rawXml },
    };

    [Fact]
    public async Task ExecuteAsync_ValidOutboundMessage_ParsesOrgObjectAndFields()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(ContextWith(SampleOutboundMessage), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        var output = (ParseOutboundMessageOutput)result.OutputData!;
        output.Parsed.ShouldBeTrue();
        output.OrganizationId.ShouldBe("00Dxx0000000001");
        output.ObjectType.ShouldBe("Lead");
        output.RecordId.ShouldBe("00Qxx0000000001");
        output.Fields["LastName"].ShouldBe("Smith");
        output.Fields["Company"].ShouldBe("Acme");
    }

    [Fact]
    public async Task ExecuteAsync_MalformedXml_ReturnsParsedFalseRatherThanThrowing()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(ContextWith("<not><valid"), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        ((ParseOutboundMessageOutput)result.OutputData!).Parsed.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ValidXmlButNotAnOutboundMessage_ReturnsParsedFalse()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(ContextWith("<somethingElse><foo>bar</foo></somethingElse>"), CancellationToken.None);

        ((ParseOutboundMessageOutput)result.OutputData!).Parsed.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_EmptyRawXml_FailsValidation()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(ContextWith(""), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }
}
