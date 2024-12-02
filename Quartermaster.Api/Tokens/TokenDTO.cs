using System;

namespace Quartermaster.Api.Tokens;

public class TokenDTO {
    public string Content { get; set; } = "";
    public DateTime? Expires { get; set; }
    public ExtendTypeDTO ExtendType { get; set; }
}

public enum ExtendTypeDTO {
    /// <summary> Specifies a Token cannot be extended at all. </summary>
    None,
    /// <summary> Specifies a Token can be extended without renewed authentication. </summary>
    Usage,
    /// <summary> Specifies a Token can be extended but the user must re-enter their Password. </summary>
    Password
}