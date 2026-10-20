using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions;

/// <summary>
/// Serves the Blazor <c>index.html</c> for motion detail pages with the motion title in <c>&lt;title&gt;</c>.
/// Link unfurlers (e.g. Discourse onebox) don't execute JavaScript, so the client-side
/// <c>PageTitle</c> never reaches them.
/// </summary>
public static class AssemblyMotionIndexPage {
    private const string IndexFile = "index.html";
    private const string DefaultTitleTag = "<title>Quartermaster</title>";

    private static readonly HtmlEncoder TitleEncoder = HtmlEncoder.Create(UnicodeRanges.All);

    public static void MapAssemblyMotionIndexPage(this IEndpointRouteBuilder endpoints) {
        endpoints.MapGet("/AssemblyMotions/{id:guid}", RenderAsync);
    }

    private static async Task<IResult> RenderAsync(
        Guid id,
        HttpContext context,
        IWebHostEnvironment env,
        I18nService i18n,
        AssemblyMotionRepository motions,
        AssemblyMotionPortalRepository portals,
        AssemblyMotionPublicAccess access) {
        var file = env.WebRootFileProvider.GetFileInfo(IndexFile);
        if (!file.Exists)
            return Results.NotFound();

        using var reader = new StreamReader(file.CreateReadStream());
        var html = await reader.ReadToEndAsync();

        var title = BuildTitle(id, i18n, motions, portals, access);
        if (title != null)
            html = html.Replace(DefaultTitleTag, $"<title>{TitleEncoder.Encode(title)}</title>");

        // The title depends on the viewer's permissions, so shared caches must not store it.
        context.Response.Headers.CacheControl = "private, no-cache";
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static string? BuildTitle(
        Guid id,
        I18nService i18n,
        AssemblyMotionRepository motions,
        AssemblyMotionPortalRepository portals,
        AssemblyMotionPublicAccess access) {
        var motion = motions.Get(id);
        if (motion == null || access.GetPortal(motion) == null)
            return null;

        var suffix = i18n[I18nKey.Ui.AssemblyMotion.DetailPageTitle];
        var type = portals.GetType(motion.TypeId);
        if (type == null || motion.Number == null)
            return $"{motion.Title} - {suffix}";

        return $"{AssemblyMotionNumber.Format(type.Code, motion.Number.Value)}: {motion.Title} - {suffix}";
    }
}
