using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.SupportSignatures;

/// <summary>The form PDF of each campaign under <c>data/support-signatures/{campaignId}.pdf</c>, named by the id so user input never reaches a path.</summary>
public class SupportSignatureFormStorage {
    private readonly string _root;

    public SupportSignatureFormStorage() : this(Path.Combine(AppContext.BaseDirectory, "data", "support-signatures")) { }

    public SupportSignatureFormStorage(string root) {
        _root = root;
    }

    public async Task SaveAsync(Guid campaignId, Stream pdf, CancellationToken ct) {
        Directory.CreateDirectory(_root);
        var path = PathOf(campaignId);
        var temporary = $"{path}.{Guid.NewGuid():N}.upload";
        try {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write)) {
                await pdf.CopyToAsync(file, ct);
            }
            File.Move(temporary, path, overwrite: true);
        } finally {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public Stream? OpenRead(Guid campaignId) {
        var path = PathOf(campaignId);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void Delete(Guid campaignId) {
        var path = PathOf(campaignId);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string PathOf(Guid campaignId) {
        return Path.Combine(_root, campaignId.ToString("N") + ".pdf");
    }
}
