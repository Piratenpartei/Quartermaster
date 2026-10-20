using System;

namespace Quartermaster.Server.Forwarding.Redmine;

public class RedmineException : Exception {
    public const string Unauthorized = "unauthorized";
    public const string NotFound = "not_found";
    public const string Rejected = "rejected";
    public const string InsecureUrl = "insecure_url";
    public const string HttpError = "http_error";
    public const string Timeout = "timeout";
    public const string InvalidResponse = "invalid_response";
    public const string NotConfigured = "not_configured";

    public string Code { get; }
    public int? StatusCode { get; }

    public RedmineException(string message, string code, int? statusCode = null, Exception? inner = null)
        : base(message, inner) {
        Code = code;
        StatusCode = statusCode;
    }
}
