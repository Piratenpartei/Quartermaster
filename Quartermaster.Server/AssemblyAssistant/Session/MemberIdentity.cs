using Quartermaster.Server.AssemblyAssistant.Mumble;

namespace Quartermaster.Server.AssemblyAssistant.Session;

/// <summary>
/// Recognises a member across reconnects, which give them a new session id: by registered user id, otherwise by
/// certificate hash, otherwise by name.
/// </summary>
public record MemberIdentity(int? UserId, string? CertificateHash, string Name) {
    public string Key { get; } = UserId != null ? $"u:{UserId}" : CertificateHash != null ? $"h:{CertificateHash}" : $"n:{Name}";

    public static MemberIdentity Of(MumbleUser user) {
        return new MemberIdentity(user.UserId, user.CertificateHash, user.Name);
    }
}
