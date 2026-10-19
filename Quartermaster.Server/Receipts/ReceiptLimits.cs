using System.Globalization;
using Quartermaster.Api;
using Quartermaster.Data.Options;
using Quartermaster.Data.Receipts;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Receipts;

public class ReceiptLimits {
    private const int DefaultMaxCount = 100;
    private const int DefaultMaxFileMegabytes = 10;

    private readonly OptionRepository _options;
    private readonly PermissionContext _perms;

    public ReceiptLimits(OptionRepository options, PermissionContext perms) {
        _options = options;
        _perms = perms;
    }

    /// <summary>Null when the current user may keep any number of receipts.</summary>
    public int? MaxCount() {
        if (_perms.HasGlobal(PermissionIdentifier.UnlimitedReceipts))
            return null;

        return ReadPositive(ReceiptOptions.MaxPerUser, DefaultMaxCount);
    }

    public int MaxFileMegabytes() {
        return ReadPositive(ReceiptOptions.MaxFileMegabytes, DefaultMaxFileMegabytes);
    }

    private int ReadPositive(string key, int fallback) {
        var raw = _options.GetGlobalValue(key)?.Value;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : fallback;
    }
}
