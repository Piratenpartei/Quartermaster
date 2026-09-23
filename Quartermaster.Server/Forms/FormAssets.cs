using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.Forms;

/// <summary>
/// Loads static assets shared by form PDF renderers (currently just the Pirate Party logo SVG) once
/// at startup so renderers don't have to know about the web-root path. Registered as a singleton.
/// </summary>
public class FormAssets {
    public string? PirateLogoSvg { get; }

    private readonly Dictionary<string, string> _byPath;

    public FormAssets(IWebHostEnvironment env, ILogger<FormAssets> logger) {
        _byPath = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(env.WebRootPath ?? "wwwroot", "assets", "pp-logo.svg");
        if (File.Exists(path)) {
            PirateLogoSvg = File.ReadAllText(path);
            _byPath["assets/pp-logo.svg"] = PirateLogoSvg;
        } else {
            logger.LogWarning("PP logo SVG not found at {Path}; form PDFs will render without it", path);
        }
    }

    /// <summary>Resolves a template <c>&lt;svg path="..." /&gt;</c> reference to the embedded SVG content. Returns null when unknown.</summary>
    public string? Resolve(string relativePath) {
        return _byPath.TryGetValue(relativePath, out var content) ? content : null;
    }
}
