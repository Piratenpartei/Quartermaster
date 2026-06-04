using Quartermaster.Data.Options;

namespace Quartermaster.Server.Email;

/// <summary>Resolved IMAP settings from <see cref="OptionRepository"/>. Null when host/credentials aren't configured.</summary>
public record ImapConfig(
    string Host,
    int Port,
    bool UseSsl,
    string Username,
    string Password,
    string Mailbox,
    int PollIntervalMinutes
) {
    public static ImapConfig? ReadFrom(OptionRepository optionRepo) {
        var host = optionRepo.GetGlobalValue("email.imap.host")?.Value;
        var username = optionRepo.GetGlobalValue("email.imap.username")?.Value;
        var password = optionRepo.GetGlobalValue("email.imap.password")?.Value;
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return null;

        var portStr = optionRepo.GetGlobalValue("email.imap.port")?.Value ?? "993";
        if (!int.TryParse(portStr, out var port))
            port = 993;

        var useSsl = optionRepo.GetGlobalValue("email.imap.use_ssl")?.Value
            ?.Equals("true", System.StringComparison.OrdinalIgnoreCase) ?? true;

        var mailbox = optionRepo.GetGlobalValue("email.imap.mailbox")?.Value;
        if (string.IsNullOrWhiteSpace(mailbox))
            mailbox = "INBOX";

        var intervalStr = optionRepo.GetGlobalValue("email.imap.poll_interval_minutes")?.Value ?? "15";
        if (!int.TryParse(intervalStr, out var interval) || interval < 1)
            interval = 15;

        return new ImapConfig(host, port, useSsl, username, password, mailbox, interval);
    }
}
