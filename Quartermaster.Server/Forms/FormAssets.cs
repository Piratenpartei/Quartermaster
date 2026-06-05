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

    public FormAssets(IWebHostEnvironment env, ILogger<FormAssets> logger) {
        var path = Path.Combine(env.WebRootPath ?? "wwwroot", "assets", "pp-logo.svg");
        if (File.Exists(path)) {
            PirateLogoSvg = File.ReadAllText(path);
        } else {
            logger.LogWarning("PP logo SVG not found at {Path}; form PDFs will render without it", path);
        }
    }
}
