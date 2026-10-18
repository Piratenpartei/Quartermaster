using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.Settings;

namespace Quartermaster.Server.Settings;

/// <summary>A connection check for one settings assistant. Runs against the saved option values and never changes anything remote.</summary>
public interface ISettingsCheck {
    SettingsCheckKind Kind { get; }

    Task<SettingsCheckResultDTO> RunAsync(CancellationToken ct);
}
