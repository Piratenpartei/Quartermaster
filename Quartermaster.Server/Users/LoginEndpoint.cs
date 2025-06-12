using FastEndpoints;
using Quartermaster.Api.Tokens;
using Quartermaster.Api.Users;
using Quartermaster.Data;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.Users;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.Users;

public class LoginEndpoint : Endpoint<LoginRequest, TokenDTO> {
    private readonly UserRepository _userRepository;
    private readonly TokenRepository _tokenRepository;

    public LoginEndpoint(UserRepository userRepository, TokenRepository tokenRepository) {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
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
            await SendAsync(_tokenRepository.LoginUser(user.Id).ToDto(), cancellation: ct);
        } else {
            await SendUnauthorizedAsync(ct);
        }
    }
}