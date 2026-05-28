using System;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Email;

/// <summary>Resolved SMTP settings from <see cref="OptionRepository"/>. Null when host/sender aren't configured.</summary>
public record SmtpConfig(
    string Host,
    int Port,
    string? Username,
    string? Password,
    string SenderAddress,
    string SenderName,
    bool UseSsl
) {
    public static SmtpConfig? ReadFrom(OptionRepository optionRepo) {
        var host = optionRepo.GetGlobalValue("email.smtp.host")?.Value;
        var senderAddress = optionRepo.GetGlobalValue("email.smtp.sender_address")?.Value;
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(senderAddress)) {
            return null;
        }

        var portStr = optionRepo.GetGlobalValue("email.smtp.port")?.Value ?? "587";
        if (!int.TryParse(portStr, out var port)) {
            port = 587;
        }

        return new SmtpConfig(
            host,
            port,
            optionRepo.GetGlobalValue("email.smtp.username")?.Value,
            optionRepo.GetGlobalValue("email.smtp.password")?.Value,
            senderAddress,
            optionRepo.GetGlobalValue("email.smtp.sender_name")?.Value ?? "Quartermaster",
            optionRepo.GetGlobalValue("email.smtp.use_ssl")?.Value?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true
        );
    }
}
