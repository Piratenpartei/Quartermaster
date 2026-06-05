using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.Banking;

/// <summary>
/// Maps a German Bankleitzahl to its BIC using a snapshot of the Bundesbank
/// Bankleitzahlendatei bundled with the server (see <c>Quartermaster.Server/Banking/README.md</c>
/// for source and attribution). Lookups are best-effort; foreign IBANs and unknown BLZs return null.
/// Registered as a singleton, the file is parsed once at startup.
/// </summary>
public class BankCodeBicLookup {
    private const string DataFileName = "blz-aktuell.txt";

    private readonly Dictionary<string, string> _bicByBlz;
    private readonly ILogger<BankCodeBicLookup> _logger;

    public BankCodeBicLookup(ILogger<BankCodeBicLookup> logger) {
        _logger = logger;
        _bicByBlz = LoadFile();
        _logger.LogInformation("Loaded {Count} BLZ/BIC entries from {File}", _bicByBlz.Count, DataFileName);
    }

    /// <summary>Returns the BIC for the German IBAN, or null when the IBAN is not German, malformed, or the bank code is unknown.</summary>
    public string? LookupByIban(string? iban) {
        if (string.IsNullOrWhiteSpace(iban)) {
            return null;
        }
        var normalised = Normalise(iban);
        if (normalised.Length != 22 || !normalised.StartsWith("DE", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }
        var blz = normalised.Substring(4, 8);
        return _bicByBlz.TryGetValue(blz, out var bic) ? bic : null;
    }

    private static string Normalise(string iban) {
        var sb = new StringBuilder(iban.Length);
        foreach (var ch in iban) {
            if (!char.IsWhiteSpace(ch)) {
                sb.Append(char.ToUpperInvariant(ch));
            }
        }
        return sb.ToString();
    }

    private Dictionary<string, string> LoadFile() {
        var path = Path.Combine(AppContext.BaseDirectory, "Banking", DataFileName);
        if (!File.Exists(path)) {
            _logger.LogWarning("BLZ data file not found at {Path}; BIC lookup will return null for every IBAN", path);
            return new Dictionary<string, string>();
        }

        var map = new Dictionary<string, string>();
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.GetEncoding("ISO-8859-1"));
        string? line;
        while ((line = reader.ReadLine()) != null) {
            if (line.Length < 150) {
                continue;
            }
            // Field 2 at position 9 (0-indexed 8): only consume the primary record per BLZ.
            if (line[8] != '1') {
                continue;
            }
            var blz = line.Substring(0, 8).Trim();
            var bic = line.Substring(139, 11).Trim();
            if (blz.Length == 8 && bic.Length > 0) {
                map[blz] = bic;
            }
        }
        return map;
    }
}
