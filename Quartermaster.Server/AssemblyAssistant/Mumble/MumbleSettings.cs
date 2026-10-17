using System;
using System.Collections.Generic;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

public record MumbleSettings(string Host, int Port, string Username, string CertificateBase64, string CertificatePassword,
    string? ServerCertificateSha256, TimeSpan VoiceHangover, IReadOnlyDictionary<ChannelRole, uint> Channels) {

    public static readonly IReadOnlyDictionary<ChannelRole, string> ChannelOptionIds = new Dictionary<ChannelRole, string> {
        [ChannelRole.Chair] = "mumble.channels.chair",
        [ChannelRole.Speaker] = "mumble.channels.speaker",
        [ChannelRole.MotionAuthor] = "mumble.channels.motion_author",
        [ChannelRole.Pro] = "mumble.channels.pro",
        [ChannelRole.Contra] = "mumble.channels.contra",
        [ChannelRole.Question] = "mumble.channels.question",
        [ChannelRole.Point] = "mumble.channels.point",
        [ChannelRole.Listener] = "mumble.channels.listener"
    };

    public bool IsConfigured() {
        if (string.IsNullOrWhiteSpace(Host))
            return false;
        if (Port <= 0 || Port > 65535)
            return false;
        if (string.IsNullOrWhiteSpace(Username))
            return false;

        return !string.IsNullOrWhiteSpace(CertificateBase64);
    }

    public static MumbleSettings ReadFrom(OptionRepository options) {
        string Value(string id) {
            var value = options.GetGlobalValue(id)?.Value;
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }

        var channels = new Dictionary<ChannelRole, uint>();
        foreach (var (role, optionId) in ChannelOptionIds) {
            if (uint.TryParse(Value(optionId), out var channelId))
                channels[role] = channelId;
        }

        var port = int.TryParse(Value("mumble.port"), out var parsedPort) ? parsedPort : 64738;
        var hangoverMs = int.TryParse(Value("assembly.voice_hangover_ms"), out var parsedHangover) && parsedHangover > 0 ? parsedHangover : 500;
        var pin = Value("mumble.server_certificate_sha256");

        return new MumbleSettings(
            Value("mumble.host"),
            port,
            Value("mumble.username"),
            Value("mumble.certificate"),
            Value("mumble.certificate_password"),
            pin.Length == 0 ? null : pin,
            TimeSpan.FromMilliseconds(hangoverMs),
            channels);
    }

    // The generated record ToString would print the certificate and its password.
    public override string ToString() {
        return $"MumbleSettings {{ Host = {Host}, Port = {Port}, Username = {Username} }}";
    }
}
