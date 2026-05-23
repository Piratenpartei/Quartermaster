using System;
using System.Text;
using System.Threading.Tasks;
using Quartermaster.Server.Users;

namespace Quartermaster.Server.Tests.Users;

public class SamlAssertionParserTests {
    private const string AudienceUrn = "https://quartermaster.example/saml";
    private const string DestinationUrn = "https://quartermaster.example/api/users/SamlConsume";
    private const string AssertionId = "_abc123-fixture";

    private static string BuildSamlResponseBase64(
        string assertionId = AssertionId,
        string notBefore = "2026-05-23T08:00:00Z",
        string notOnOrAfter = "2026-05-23T08:05:00Z",
        string? audience = AudienceUrn,
        string? destination = DestinationUrn,
        bool includeAssertion = true,
        bool includeConditions = true) {

        var destAttr = destination is null ? "" : $" Destination=\"{destination}\"";
        var audienceXml = audience is null
            ? ""
            : $"<saml:AudienceRestriction><saml:Audience>{audience}</saml:Audience></saml:AudienceRestriction>";
        var conditionsXml = includeConditions
            ? $"<saml:Conditions NotBefore=\"{notBefore}\" NotOnOrAfter=\"{notOnOrAfter}\">{audienceXml}</saml:Conditions>"
            : "";
        var assertionXml = includeAssertion
            ? $"<saml:Assertion ID=\"{assertionId}\" Version=\"2.0\" IssueInstant=\"{notBefore}\">{conditionsXml}</saml:Assertion>"
            : "";
        var xml = $"""
            <samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"{destAttr}>
                {assertionXml}
            </samlp:Response>
            """;
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
    }

    [Test]
    public async Task Parses_all_fields_from_well_formed_response() {
        var meta = SamlAssertionParser.TryParse(BuildSamlResponseBase64());
        await Assert.That(meta).IsNotNull();
        await Assert.That(meta!.AssertionId).IsEqualTo(AssertionId);
        await Assert.That(meta.NotBefore).IsEqualTo(DateTime.Parse("2026-05-23T08:00:00Z").ToUniversalTime());
        await Assert.That(meta.NotOnOrAfter).IsEqualTo(DateTime.Parse("2026-05-23T08:05:00Z").ToUniversalTime());
        await Assert.That(meta.Audience).IsEqualTo(AudienceUrn);
        await Assert.That(meta.Destination).IsEqualTo(DestinationUrn);
    }

    [Test]
    public async Task Returns_null_for_empty_or_malformed_base64() {
        await Assert.That(SamlAssertionParser.TryParse("")).IsNull();
        await Assert.That(SamlAssertionParser.TryParse("not-base64!!!")).IsNull();
    }

    [Test]
    public async Task Returns_null_for_non_xml_payload() {
        var notXml = Convert.ToBase64String(Encoding.UTF8.GetBytes("hello world"));
        await Assert.That(SamlAssertionParser.TryParse(notXml)).IsNull();
    }

    [Test]
    public async Task Returns_null_when_assertion_element_missing() {
        await Assert.That(SamlAssertionParser.TryParse(BuildSamlResponseBase64(includeAssertion: false))).IsNull();
    }

    [Test]
    public async Task Returns_null_when_assertion_id_missing() {
        await Assert.That(SamlAssertionParser.TryParse(BuildSamlResponseBase64(assertionId: ""))).IsNull();
    }

    [Test]
    public async Task Returns_null_when_conditions_element_missing() {
        await Assert.That(SamlAssertionParser.TryParse(BuildSamlResponseBase64(includeConditions: false))).IsNull();
    }

    [Test]
    public async Task Returns_null_when_timestamps_unparseable() {
        await Assert.That(SamlAssertionParser.TryParse(BuildSamlResponseBase64(notBefore: "not-a-date"))).IsNull();
        await Assert.That(SamlAssertionParser.TryParse(BuildSamlResponseBase64(notOnOrAfter: "not-a-date"))).IsNull();
    }

    [Test]
    public async Task Optional_audience_and_destination_return_null_when_absent() {
        var meta = SamlAssertionParser.TryParse(BuildSamlResponseBase64(audience: null, destination: null));
        await Assert.That(meta).IsNotNull();
        await Assert.That(meta!.Audience).IsNull();
        await Assert.That(meta.Destination).IsNull();
    }
}
