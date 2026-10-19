using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.Receipts;

/// <summary>Receipt files under <c>data/receipts/{userId}/</c>, named by the receipt id so user input never reaches a path.</summary>
public class ReceiptStorage {
    private readonly string _root;

    public ReceiptStorage() : this(Path.Combine(AppContext.BaseDirectory, "data", "receipts")) { }

    public ReceiptStorage(string root) {
        _root = root;
    }

    public async Task<string> SaveAsync(Guid ownerUserId, Guid receiptId, ReceiptFileType type, Stream content, CancellationToken ct) {
        var folder = Path.Combine(_root, ownerUserId.ToString("N"));
        Directory.CreateDirectory(folder);
        var storedName = receiptId.ToString("N") + type.Extension;
        await using var file = new FileStream(Path.Combine(folder, storedName), FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, ct);
        return storedName;
    }

    public string PathOf(Guid ownerUserId, string storedName) {
        return Path.Combine(_root, ownerUserId.ToString("N"), Path.GetFileName(storedName));
    }

    public void Delete(Guid ownerUserId, string storedName) {
        var path = PathOf(ownerUserId, storedName);
        if (File.Exists(path))
            File.Delete(path);
    }
}
