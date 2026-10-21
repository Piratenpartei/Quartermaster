namespace Quartermaster.Server.SupportSignatures;

public record SupportSignatureOutcome(bool Succeeded, int StatusCode, string? ErrorKey) {
    public static SupportSignatureOutcome Ok() {
        return new SupportSignatureOutcome(true, 200, null);
    }

    public static SupportSignatureOutcome Fail(int statusCode, string errorKey) {
        return new SupportSignatureOutcome(false, statusCode, errorKey);
    }
}
