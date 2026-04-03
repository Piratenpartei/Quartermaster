using FastEndpoints;
using Quartermaster.Api.Users;
using Quartermaster.Data;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.Users;

public class LoginEndpoint : Endpoint<LoginRequest, LoginResponse> {
    private readonly UserRepository _userRepository;
    private readonly TokenRepository _tokenRepository;
    private readonly UserGlobalPermissionRepository _globalPermissionRepository;
    private readonly UserChapterPermissionRepository _chapterPermissionRepository;

    public LoginEndpoint(UserRepository userRepository, TokenRepository tokenRepository,
        UserGlobalPermissionRepository globalPermissionRepository,
        UserChapterPermissionRepository chapterPermissionRepository) {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _globalPermissionRepository = globalPermissionRepository;
        _chapterPermissionRepository = chapterPermissionRepository;
    }

    public override void Configure() {
        Post("/api/users/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct) {
        const string RndPw = "EE83C9600AA859921DC735E46DCAC5F83B7B1A7BDB0256524FEE6CFC9183930656F763FCB7D0AB" +
            "021CCB025F86F04EF0DC29DA022FA923576CE4FE832B78E850;031DAE440EF21E786C7ECF5B064C1B73;500000;SHA512";
        var user = _userRepository.GetByUsername(req.Username!);

        if (PasswordHashser.Verify(req.Password, user?.PasswordHash ?? RndPw) && user != null) {
            var token = _tokenRepository.LoginUser(user.Id);

            var globalPermissions = _globalPermissionRepository.GetForUser(user.Id)
                .Select(p => p.Identifier)
                .ToList();

            var chapterPermissions = _chapterPermissionRepository.GetAllForUser(user.Id);

            var response = new LoginResponse {
                Token = token.Content,
                Expires = token.Expires,
                User = new LoginUserInfo {
                    Id = user.Id,
                    Username = user.Username ?? "",
                    DisplayName = BuildDisplayName(user),
                    EMail = user.EMail
                },
                Permissions = new LoginPermissions {
                    Global = globalPermissions,
                    Chapters = chapterPermissions.ToDictionary(
                        kvp => kvp.Key.ToString(),
                        kvp => kvp.Value)
                }
            };

            await SendAsync(response, cancellation: ct);
        } else {
            await SendUnauthorizedAsync(ct);
        }
    }

    private static string BuildDisplayName(Data.Users.User user) {
        if (!string.IsNullOrEmpty(user.FirstName) && !string.IsNullOrEmpty(user.LastName))
            return $"{user.FirstName} {user.LastName}";

        if (!string.IsNullOrEmpty(user.Username))
            return user.Username;

        return user.EMail;
    }
}
