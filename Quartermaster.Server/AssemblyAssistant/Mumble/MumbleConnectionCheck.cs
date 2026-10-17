using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyAssistant.Session;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

/// <summary>
/// Short-lived connection for the setup page: connects with the stored options, reads the server state, optionally
/// registers the bot, and disconnects again. The bot stays in the channel the server puts it in.
/// </summary>
public class MumbleConnectionCheck {
    private static readonly TimeSpan SyncTimeout = TimeSpan.FromSeconds(10);
    private static readonly ChannelRole[] ChairLinkedRoles = [ChannelRole.Speaker, ChannelRole.MotionAuthor, ChannelRole.Listener];

    private readonly IMumbleConnectionFactory _factory;
    private readonly OptionRepository _options;
    private readonly AssemblySessionHost _sessions;

    public MumbleConnectionCheck(IMumbleConnectionFactory factory, OptionRepository options, AssemblySessionHost sessions) {
        _factory = factory;
        _options = options;
        _sessions = sessions;
    }

    /// <summary>Refused while a session runs: a second connection with the same certificate would push the live bot off the server.</summary>
    public async Task<AssemblySetupCheckDTO> CheckAsync(CancellationToken ct) {
        if (_sessions.IsRunning)
            return new AssemblySetupCheckDTO { ErrorKey = I18nKey.Ui.AssemblySetup.Error.SessionRunning };

        var settings = MumbleSettings.ReadFrom(_options);
        if (!settings.IsConfigured())
            return new AssemblySetupCheckDTO { ErrorKey = I18nKey.Ui.AssemblySetup.Error.NotConfigured };

        await using var connection = _factory.Create();
        var outcome = await ConnectAsync(connection, settings, ct);
        var result = new AssemblySetupCheckDTO {
            Connected = outcome.Snapshot != null,
            ErrorKey = outcome.ErrorKey,
            ErrorDetail = outcome.ErrorDetail,
            ServerVersion = connection.ServerVersion,
            ServerCertificateSha256 = connection.LastServerCertificateSha256
        };

        if (outcome.Snapshot != null) {
            var snapshot = connection.Snapshot();
            result.Registered = snapshot.Users.Any(u => u.Session == snapshot.OwnSession && u.UserId != null);
            result.Channels = snapshot.Channels
                .Select(c => new AssemblySetupChannelDTO {
                    Id = c.Id,
                    ParentId = c.ParentId,
                    Name = c.Name,
                    Links = c.Links.OrderBy(l => l).ToList(),
                    UserCount = snapshot.Users.Count(u => u.ChannelId == c.Id)
                })
                .ToList();
            result.Problems = FindProblems(snapshot, settings.Channels);
        }

        await connection.StopAsync();
        return result;
    }

    public async Task<AssemblySetupRegisterDTO> RegisterAsync(CancellationToken ct) {
        if (_sessions.IsRunning)
            return new AssemblySetupRegisterDTO { ErrorKey = I18nKey.Ui.AssemblySetup.Error.SessionRunning };

        var settings = MumbleSettings.ReadFrom(_options);
        if (!settings.IsConfigured())
            return new AssemblySetupRegisterDTO { ErrorKey = I18nKey.Ui.AssemblySetup.Error.NotConfigured };

        await using var connection = _factory.Create();
        var outcome = await ConnectAsync(connection, settings, ct);
        if (outcome.Snapshot == null)
            return new AssemblySetupRegisterDTO { ErrorKey = outcome.ErrorKey, ErrorDetail = outcome.ErrorDetail };

        var registered = await connection.RegisterSelfAsync(ct);
        await connection.StopAsync();
        return registered
            ? new AssemblySetupRegisterDTO { Registered = true }
            : new AssemblySetupRegisterDTO { ErrorKey = I18nKey.Ui.AssemblySetup.Error.RegisterFailed };
    }

    public static List<AssemblySetupProblemDTO> FindProblems(MumbleServerSnapshot snapshot, IReadOnlyDictionary<ChannelRole, uint> mapping) {
        var problems = new List<AssemblySetupProblemDTO>();
        var channelIds = snapshot.Channels.Select(c => c.Id).ToHashSet();

        foreach (var role in Enum.GetValues<ChannelRole>()) {
            if (!mapping.TryGetValue(role, out var channelId)) {
                problems.Add(new AssemblySetupProblemDTO { Role = role, MessageKey = I18nKey.Ui.AssemblySetup.Problem.Unmapped });
            } else if (!channelIds.Contains(channelId)) {
                problems.Add(new AssemblySetupProblemDTO { Role = role, MessageKey = I18nKey.Ui.AssemblySetup.Problem.ChannelMissing });
            }
        }

        var duplicates = mapping
            .GroupBy(m => m.Value)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Select(m => m.Key));
        foreach (var role in duplicates)
            problems.Add(new AssemblySetupProblemDTO { Role = role, MessageKey = I18nKey.Ui.AssemblySetup.Problem.DuplicateChannel });

        if (!mapping.TryGetValue(ChannelRole.Chair, out var chairId) || !channelIds.Contains(chairId))
            return problems;

        var reachable = LinkClosure(snapshot, chairId);
        foreach (var role in ChairLinkedRoles) {
            if (mapping.TryGetValue(role, out var channelId) && channelIds.Contains(channelId) && !reachable.Contains(channelId))
                problems.Add(new AssemblySetupProblemDTO { Role = role, MessageKey = I18nKey.Ui.AssemblySetup.Problem.NotLinkedToChair });
        }

        return problems;
    }

    // Murmur delivers voice into every channel reachable through links, not only direct neighbours.
    private static HashSet<uint> LinkClosure(MumbleServerSnapshot snapshot, uint start) {
        var linksById = snapshot.Channels.ToDictionary(c => c.Id, c => c.Links);
        var seen = new HashSet<uint> { start };
        var stack = new Stack<uint>();
        stack.Push(start);

        while (stack.Count > 0) {
            var current = stack.Pop();
            if (!linksById.TryGetValue(current, out var links))
                continue;

            foreach (var next in links) {
                if (seen.Add(next))
                    stack.Push(next);
            }
        }

        return seen;
    }

    private static async Task<ConnectOutcome> ConnectAsync(IMumbleConnection connection, MumbleSettings settings, CancellationToken ct) {
        var done = new TaskCompletionSource<ConnectOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.EventRaised += mumbleEvent => {
            if (mumbleEvent is MumbleSynced synced)
                done.TrySetResult(new ConnectOutcome(synced.Snapshot, null, null));
            if (mumbleEvent is MumbleConnectionChanged changed && changed.State == MumbleConnectionState.Reconnecting && changed.Error != null)
                done.TrySetResult(new ConnectOutcome(null, I18nKey.Ui.AssemblySetup.Error.ConnectionFailed, changed.Error));
        };

        await connection.StartAsync(settings, null, ct);
        var finished = await Task.WhenAny(done.Task, Task.Delay(SyncTimeout, ct));
        if (finished == done.Task)
            return await done.Task;

        return new ConnectOutcome(null, I18nKey.Ui.AssemblySetup.Error.Timeout, null);
    }

    private record ConnectOutcome(MumbleServerSnapshot? Snapshot, string? ErrorKey, string? ErrorDetail);
}
