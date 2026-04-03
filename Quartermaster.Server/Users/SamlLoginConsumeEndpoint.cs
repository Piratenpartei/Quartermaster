using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.Users;

namespace Quartermaster.Server.Users;

public class SamlLoginConsumeEndpoint : Endpoint<SamlLoginRequest, EmptyResponse> {
    private readonly OptionRepository _optionRepo;
    private readonly UserRepository _userRepo;
    private readonly MemberRepository _memberRepo;
    private readonly TokenRepository _tokenRepo;

    public SamlLoginConsumeEndpoint(
        OptionRepository optionRepo,
        UserRepository userRepo,
        MemberRepository memberRepo,
        TokenRepository tokenRepo) {
        _optionRepo = optionRepo;
        _userRepo = userRepo;
        _memberRepo = memberRepo;
        _tokenRepo = tokenRepo;
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

        Logger.LogInformation("SAML login attempt for email: {Email}", email);

        // Find member by email — SSO login requires an existing member
        var member = _memberRepo.GetByEmail(email);
        if (member == null) {
            await SendRedirectAsync("/Login?error=saml_no_member", allowRemoteRedirects: false);
            return;
        }

        // Block exited members
        if (member.ExitDate.HasValue) {
            await SendRedirectAsync("/Login?error=saml_member_exited", allowRemoteRedirects: false);
            return;
        }

        // Find or create user, link to member
        var user = member.UserId.HasValue ? _userRepo.GetById(member.UserId.Value) : null;

        if (user == null) {
            // Also check if a user with this email already exists (e.g., manually created)
            user = _userRepo.GetByEmail(email);

            if (user == null) {
                user = new User {
                    EMail = email,
                    Username = email,
                    FirstName = member.FirstName ?? "",
                    LastName = member.LastName ?? ""
                };
                _userRepo.Create(user);
            }

            // Link member to user
            _memberRepo.SetUserId(member.Id, user.Id);
        }

        // Block soft-deleted users
        if (user.DeletedAt.HasValue) {
            await SendRedirectAsync("/Login?error=saml_member_exited", allowRemoteRedirects: false);
            return;
        }

        var token = _tokenRepo.LoginUser(user.Id);

        // Redirect to Blazor callback with token in URL fragment (not sent to server/logs)
        await SendRedirectAsync($"/Login/SamlCallback#{token.Content}", allowRemoteRedirects: false);
    }
}

public class SamlLoginRequest {
    [BindFrom("SAMLResponse")]
    public string? SamlData { get; set; }
}
