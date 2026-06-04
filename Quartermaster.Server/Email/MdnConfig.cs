using Quartermaster.Data.Options;

namespace Quartermaster.Server.Email;

/// <summary>Read-receipt (MDN, RFC 3798) configuration resolved from <see cref="OptionRepository"/>.
/// The receipt address falls back to the SMTP sender address when unset so receipts land in the same mailbox the IMAP poller already monitors.</summary>
public record MdnConfig(bool RequestDefault, string ReceiptAddress) {
    public static MdnConfig ReadFrom(OptionRepository optionRepo, SmtpConfig? smtp) {
        var requestDefaultRaw = optionRepo.GetGlobalValue("email.mdn.request_default")?.Value;
        var requestDefault = requestDefaultRaw == null
            ? true
            : requestDefaultRaw.Equals("true", System.StringComparison.OrdinalIgnoreCase);

        var receipt = optionRepo.GetGlobalValue("email.mdn.receipt_address")?.Value;
        if (string.IsNullOrWhiteSpace(receipt))
            receipt = smtp?.SenderAddress ?? "";

        return new MdnConfig(requestDefault, receipt);
    }
}
