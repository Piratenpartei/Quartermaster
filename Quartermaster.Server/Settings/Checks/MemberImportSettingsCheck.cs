using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Settings;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Settings.Checks;

/// <summary>Reads the configured export file without importing it: exists, readable, has the member number column.</summary>
public class MemberImportSettingsCheck : ISettingsCheck {
    public const string FilePathKey = "member_import.file_path";
    public const string MemberNumberColumn = "USER_Mitgliedsnummer";

    private readonly OptionRepository _options;
    private readonly ILogger<MemberImportSettingsCheck> _logger;

    public MemberImportSettingsCheck(OptionRepository options, ILogger<MemberImportSettingsCheck> logger) {
        _options = options;
        _logger = logger;
    }

    public SettingsCheckKind Kind => SettingsCheckKind.MemberImport;

    public async Task<SettingsCheckResultDTO> RunAsync(CancellationToken ct) {
        var path = _options.GetGlobalValue(FilePathKey)?.Value?.Trim();
        if (string.IsNullOrEmpty(path))
            return SettingsCheckResults.NotConfigured();
        if (!File.Exists(path))
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.MemberImportFileMissing);

        try {
            using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var header = await reader.ReadLineAsync(ct) ?? "";
            var columns = header.Split(';').Select(c => c.Trim().Trim('"'));
            if (!columns.Contains(MemberNumberColumn))
                return SettingsCheckResults.Failed(I18nParams.With(I18nKey.Ui.SettingsCheck.MemberImportColumnMissing, ("column", MemberNumberColumn)));

            var rows = 0;
            var line = await reader.ReadLineAsync(ct);
            while (line != null) {
                if (line.Length > 0)
                    rows++;
                line = await reader.ReadLineAsync(ct);
            }

            var modified = BerlinTime.FromUtc(File.GetLastWriteTimeUtc(path)).ToString("dd.MM.yyyy HH:mm");
            return SettingsCheckResults.Ok(I18nParams.With(I18nKey.Ui.SettingsCheck.MemberImportOk,
                ("rows", rows.ToString()), ("modified", modified)));
        } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
            _logger.LogWarning("Member import settings check could not read {Path}: {Exception}", path, ex);
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.MemberImportUnreadable, ex.Message);
        }
    }
}
