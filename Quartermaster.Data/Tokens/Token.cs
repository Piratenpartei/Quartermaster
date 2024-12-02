using System;

namespace Quartermaster.Data.Tokens;

public class Token {
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Content { get; set; } = "";
    public TokenType Type { get; set; }
    public DateTime? Expires { get; set; }
    public ExtendType ExtendType { get; set; }
    public TokenSecurityScope SecurityScope { get; set; }
}

public enum TokenType {
    Login,
    DonationMarker
}

public enum ExtendType {
    /// <summary> Specifies a Token cannot be extended at all. </summary>
    None,
    /// <summary> Specifies a Token can be extended without renewed authentication. </summary>
    Usage,
    /// <summary> Specifies a Token can be extended but the user must re-enter their Password. </summary>
    Password
}

public enum TokenSecurityScope {
    None,
    IP
}