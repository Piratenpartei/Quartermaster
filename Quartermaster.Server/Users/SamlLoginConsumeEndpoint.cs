using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Quartermaster.Server.Authentication;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;
using Quartermaster.Data.Saml;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.Users;
using Quartermaster.Server.Members;

namespace Quartermaster.Server.Users;

public class SamlLoginConsumeEndpoint : Endpoint<SamlLoginRequest, EmptyResponse> {
    private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromSeconds(60);

    private static readonly string[] FirstNameAttributes = [
        "firstName",
        "givenName",
        "urn:oid:2.5.4.42",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname"
    ];

    private static readonly string[] LastNameAttributes = [
        "lastName",
        "surname",
        "sn",
        "urn:oid:2.5.4.4",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname"
    ];

    private readonly OptionRepository _optionRepo;
    private readonly UserRepository _userRepo;
    private readonly MemberRepository _memberRepo;
    private readonly TokenRepository _tokenRepo;
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly UsedSamlAssertionRepository _assertionRepo;
    private readonly KeycloakDepartmentChapterAssigner _departments;

    public SamlLoginConsumeEndpoint(
        OptionRepository optionRepo,
        UserRepository userRepo,
        MemberRepository memberRepo,
        TokenRepository tokenRepo,
        ChapterOfficerRepository officerRepo,
        UsedSamlAssertionRepository assertionRepo,
        KeycloakDepartmentChapterAssigner departments) {
        _optionRepo = optionRepo;
        _userRepo = userRepo;
        _memberRepo = memberRepo;
        _tokenRepo = tokenRepo;
        _officerRepo = officerRepo;
        _assertionRepo = assertionRepo;
        _departments = departments;
    }

    public override void Configure() {
        Post("/api/users/SamlConsume");
        AllowAnonymous();
        AllowFormData(true);
        Description(x => x.Accepts<SamlLoginRequest>("application/x-www-form-urlencoded"));
    }

    public override async Task HandleAsync(SamlLoginRequest req, CancellationToken ct) {
        if (string.IsNullOrEmpty(req.SamlData)) {
            await SendAsync(new EmptyResponse(), 400, ct);
            return;
        }

        var certBase64 = _optionRepo.GetGlobalValue("auth.saml.certificate")?.Value;
        if (string.IsNullOrEmpty(certBase64)) {
            await SendAsync(new EmptyResponse(), 503, ct);
            return;
        }

        var cert = "-----BEGIN CERTIFICATE-----\n"
            + certBase64 + "\n"
            + "-----END CERTIFICATE-----";

        Saml.Response samlResponse;
        try {
            samlResponse = new Saml.Response(cert, req.SamlData);
        } catch (Exception ex) {
            Logger.LogError(ex, "SAML response parsing failed");
            await SendRedirectAsync("/Login?error=saml_invalid", allowRemoteRedirects: false);
            return;
        }

        if (!samlResponse.IsValid()) {
            await SendRedirectAsync("/Login?error=saml_signature", allowRemoteRedirects: false);
            return;
        }

        var metadata = SamlAssertionParser.TryParse(req.SamlData);
        if (metadata == null) {
            Logger.LogWarning("SAML login failed: assertion metadata unparseable or missing required fields");
            await SendRedirectAsync("/Login?error=saml_invalid", allowRemoteRedirects: false);
            return;
        }

        var now = DateTime.UtcNow;
        if (metadata.NotBefore - ClockSkewTolerance > now || metadata.NotOnOrAfter + ClockSkewTolerance <= now) {
            Logger.LogWarning("SAML login failed: assertion outside validity window. NotBefore={NotBefore} NotOnOrAfter={NotOnOrAfter} Now={Now}",
                metadata.NotBefore, metadata.NotOnOrAfter, now);
            await SendRedirectAsync("/Login?error=saml_expired", allowRemoteRedirects: false);
            return;
        }

        var expectedAudience = _optionRepo.GetGlobalValue("auth.saml.expected_audience")?.Value;
        if (!string.IsNullOrEmpty(expectedAudience) && metadata.Audience != expectedAudience) {
            Logger.LogWarning("SAML login failed: audience mismatch. Got={Got} Expected={Expected}",
                metadata.Audience, expectedAudience);
            await SendRedirectAsync("/Login?error=saml_audience", allowRemoteRedirects: false);
            return;
        }

        var expectedDestination = _optionRepo.GetGlobalValue("auth.saml.expected_destination")?.Value;
        if (!string.IsNullOrEmpty(expectedDestination) && metadata.Destination != expectedDestination) {
            Logger.LogWarning("SAML login failed: destination mismatch. Got={Got} Expected={Expected}",
                metadata.Destination, expectedDestination);
            await SendRedirectAsync("/Login?error=saml_destination", allowRemoteRedirects: false);
            return;
        }

        if (!_assertionRepo.TryMarkUsed(metadata.AssertionId, metadata.NotOnOrAfter)) {
            Logger.LogWarning("SAML login failed: assertion replay detected. AssertionID={AssertionId}", metadata.AssertionId);
            await SendRedirectAsync("/Login?error=saml_replay", allowRemoteRedirects: false);
            return;
        }

        // Try email from NameID first, fall back to SAML attributes
        var nameId = samlResponse.GetNameID();
        var email = nameId;

        if (string.IsNullOrEmpty(email) || !email.Contains('@')) {
            email = samlResponse.GetCustomAttribute("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")
                ?? samlResponse.GetCustomAttribute("urn:oid:1.2.840.113549.1.9.1")
                ?? samlResponse.GetCustomAttribute("email")
                ?? samlResponse.GetCustomAttribute("mail")
                ?? samlResponse.GetCustomAttribute("Email");
        }

        if (string.IsNullOrEmpty(email) || !email.Contains('@')) {
            Logger.LogWarning("SAML login failed: no email found. NameID={NameID}", nameId);
            await SendRedirectAsync("/Login?error=saml_no_identity", allowRemoteRedirects: false);
            return;
        }

        var memberNumberAttribute = _optionRepo.GetGlobalValue("auth.saml.member_number_attribute")?.Value;
        if (string.IsNullOrEmpty(memberNumberAttribute))
            memberNumberAttribute = "member_number";

        var rawMemberNumber = samlResponse.GetCustomAttribute(memberNumberAttribute);
        if (!MemberNumberParser.TryParse(rawMemberNumber, out var memberNumber)) {
            Logger.LogWarning("SAML login failed: attribute {Attribute} missing or not a member number. Value={Value}",
                memberNumberAttribute, rawMemberNumber);
            await SendRedirectAsync("/Login?error=sso_no_member_number", allowRemoteRedirects: false);
            return;
        }

        var departmentAttribute = _optionRepo.GetGlobalValue("auth.saml.department_attribute")?.Value;
        if (string.IsNullOrEmpty(departmentAttribute))
            departmentAttribute = "ekklesia_department";

        var eligibleAttribute = _optionRepo.GetGlobalValue("auth.saml.eligible_attribute")?.Value;
        if (string.IsNullOrEmpty(eligibleAttribute))
            eligibleAttribute = "ekklesia_eligible";

        var identity = new SsoIdentity(email, memberNumber,
            FirstAttribute(samlResponse, FirstNameAttributes),
            FirstAttribute(samlResponse, LastNameAttributes),
            FirstAttribute(samlResponse, [departmentAttribute]),
            SsoLoginHelper.ParseEligible(FirstAttribute(samlResponse, [eligibleAttribute])));

        Logger.LogInformation("SAML login attempt for member number {MemberNumber}", memberNumber);

        var member = SsoLoginHelper.ResolveOrCreateMember(identity, _memberRepo);
        var issuedIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
        var (result, token) = SsoLoginHelper.ProcessSsoLogin(member, email,
            issuedIp,
            string.IsNullOrEmpty(userAgent) ? null : userAgent,
            _memberRepo, _userRepo, _tokenRepo, _officerRepo);

        switch (result) {
            case SsoLoginResult.MemberExited:
            case SsoLoginResult.UserDeleted:
                await SendRedirectAsync("/Login?error=saml_member_exited", allowRemoteRedirects: false);
                return;
        }

        _departments.Assign(member, identity.Department);
        AuthCookie.Set(HttpContext, token!.Content, token.Expires);
        await SendRedirectAsync("/", allowRemoteRedirects: false);
    }

    private static string? FirstAttribute(Saml.Response samlResponse, string[] names) {
        foreach (var name in names) {
            var value = samlResponse.GetCustomAttribute(name);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return null;
    }
}

public class SamlLoginRequest {
    [BindFrom("SAMLResponse")]
    public string? SamlData { get; set; }
}
