using System;
using System.Text;
using System.Xml;

namespace Quartermaster.Server.Users;

public record SamlAssertionMetadata(
    string AssertionId,
    DateTime NotBefore,
    DateTime NotOnOrAfter,
    string? Audience,
    string? Destination
);

public static class SamlAssertionParser {
    private const string SamlpNs = "urn:oasis:names:tc:SAML:2.0:protocol";
    private const string SamlNs = "urn:oasis:names:tc:SAML:2.0:assertion";

    /// <summary>
    /// Decodes the base64 SAMLResponse payload and pulls out the security-relevant assertion
    /// metadata. Returns <c>null</c> when any required field (AssertionID, NotBefore,
    /// NotOnOrAfter) is missing — the caller must treat that as a rejection. Audience and
    /// Destination are optional in the spec and returned as <c>null</c> when absent.
    /// </summary>
    public static SamlAssertionMetadata? TryParse(string base64SamlResponse) {
        if (string.IsNullOrEmpty(base64SamlResponse))
            return null;

        string xml;
        try {
            xml = Encoding.UTF8.GetString(Convert.FromBase64String(base64SamlResponse));
        } catch (FormatException) {
            return null;
        }

        var doc = new XmlDocument { PreserveWhitespace = false };
        try {
            doc.LoadXml(xml);
        } catch (XmlException) {
            return null;
        }

        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("samlp", SamlpNs);
        nsmgr.AddNamespace("saml", SamlNs);

        var destination = doc.DocumentElement?.GetAttribute("Destination");
        var assertion = doc.SelectSingleNode("/samlp:Response/saml:Assertion", nsmgr) as XmlElement;
        if (assertion == null)
            return null;

        var assertionId = assertion.GetAttribute("ID");
        if (string.IsNullOrEmpty(assertionId))
            return null;

        var conditions = assertion.SelectSingleNode("saml:Conditions", nsmgr) as XmlElement;
        if (conditions == null)
            return null;
        if (!TryParseUtc(conditions.GetAttribute("NotBefore"), out var notBefore))
            return null;
        if (!TryParseUtc(conditions.GetAttribute("NotOnOrAfter"), out var notOnOrAfter))
            return null;

        var audience = (conditions.SelectSingleNode("saml:AudienceRestriction/saml:Audience", nsmgr) as XmlElement)?.InnerText;

        return new SamlAssertionMetadata(
            AssertionId: assertionId,
            NotBefore: notBefore,
            NotOnOrAfter: notOnOrAfter,
            Audience: string.IsNullOrEmpty(audience) ? null : audience,
            Destination: string.IsNullOrEmpty(destination) ? null : destination
        );
    }

    private static bool TryParseUtc(string raw, out DateTime utc) {
        if (DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out utc)) {
            return true;
        }
        utc = default;
        return false;
    }
}
