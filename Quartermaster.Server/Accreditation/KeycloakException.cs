using System;

namespace Quartermaster.Server.Accreditation;

public class KeycloakException : Exception {
    public int? StatusCode { get; }
    public string? ErrorMessageKey { get; }

    public KeycloakException(int? statusCode, string message, Exception? inner = null, string? errorMessageKey = null)
        : base(message, inner) {
        StatusCode = statusCode;
        ErrorMessageKey = errorMessageKey;
    }
}
