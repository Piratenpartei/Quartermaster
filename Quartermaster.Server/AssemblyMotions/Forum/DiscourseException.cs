using System;

namespace Quartermaster.Server.AssemblyMotions.Forum;

public class DiscourseException : Exception {
    public const string Unauthorized = "unauthorized";
    public const string NotFound = "not_found";
    public const string Rejected = "rejected";
    public const string RateLimited = "rate_limited";
    public const string InsecureUrl = "insecure_url";
    public const string HttpError = "http_error";
    public const string Timeout = "timeout";
    public const string InvalidResponse = "invalid_response";

    public string Code { get; }
    public int? StatusCode { get; }
    public TimeSpan? RetryAfter { get; }

    public DiscourseException(string message, string code, int? statusCode = null, TimeSpan? retryAfter = null, Exception? inner = null)
        : base(message, inner) {
        Code = code;
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }
}
