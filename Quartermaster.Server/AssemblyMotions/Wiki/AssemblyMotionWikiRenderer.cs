using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Rendering;

namespace Quartermaster.Server.AssemblyMotions.Wiki;

public static class AssemblyMotionWikiRenderer {
    private const int MaxTemplateSteps = 20_000;
    private const int MaxTemplateRecursion = 20;

    // Own options instance so the limits apply to wiki pages only; the templates are admin-editable.
    private static readonly TemplateOptions Options = new() {
        MaxSteps = MaxTemplateSteps,
        MaxRecursion = MaxTemplateRecursion
    };

    /// <summary>
    /// Renders the wiki page text of one motion: NFC, LF line endings, no trailing whitespace. Throws <see cref="InvalidOperationException"/> carrying the Fluid error when the type's template is broken.
    /// </summary>
    public static async Task<string> RenderAsync(AssemblyMotionWikiInput input) {
        var template = string.IsNullOrWhiteSpace(input.Type.WikiTemplate)
            ? AssemblyMotionWikiTemplates.DefaultFor(input.Type.Code)
            : input.Type.WikiTemplate;

        string? text;
        string? error;
        try {
            (text, error) = await TemplateRenderer.RenderTextAsync(template, BuildModel(input), Options);
        } catch (Exception ex) {
            throw new InvalidOperationException($"Wiki template for type {input.Type.Code} failed to render: {ex.Message}", ex);
        }

        if (error != null)
            throw new InvalidOperationException($"Wiki template for type {input.Type.Code} is invalid: {error}");

        return WikiPageText.Normalize(text);
    }

    private static Dictionary<string, object> BuildModel(AssemblyMotionWikiInput input) {
        var motion = input.Motion;
        var number = motion.Number.HasValue
            ? AssemblyMotionNumber.Format(input.Type.Code, motion.Number.Value)
            : "";

        return new Dictionary<string, object> {
            ["assembly"] = new Dictionary<string, object> {
                ["Label"] = MotionWikiConverter.EscapeInline(input.Portal.WikiAssemblyLabel)
            },
            ["type"] = new Dictionary<string, object> {
                ["Code"] = MotionWikiConverter.EscapeInline(input.Type.Code),
                ["Name"] = MotionWikiConverter.EscapeInline(input.Type.Name)
            },
            ["motion"] = new Dictionary<string, object> {
                ["Number"] = MotionWikiConverter.EscapeInline(number),
                ["Title"] = MotionWikiConverter.EscapeInline(motion.Title),
                ["Section"] = MotionWikiConverter.EscapeInline(motion.Section),
                ["Text"] = MotionWikiConverter.ToWikitext(motion.TextMarkdown),
                ["Reason"] = MotionWikiConverter.ToWikitext(motion.ReasonMarkdown),
                ["Summary"] = MotionWikiConverter.EscapeInline(motion.Summary),
                ["Keywords"] = MotionWikiConverter.EscapeInline(motion.Keywords),
                ["PriorDiscussion"] = MotionWikiConverter.EscapeInline(motion.PriorDiscussion),
                ["CompetingNumbers"] = MotionWikiConverter.EscapeInline(string.Join(", ", input.CompetingDisplayNumbers)),
                ["CheckIcon"] = AssemblyMotionWikiStatus.CheckIcon(motion, input.HasOpenInquiry),
                ["VoteResult"] = AssemblyMotionWikiStatus.VoteResult(motion),
                ["PortalUrl"] = input.PortalUrl,
                ["InRevision"] = motion.IsInRevision
            },
            ["bot"] = new Dictionary<string, object> {
                ["UserName"] = input.BotUserName
            }
        };
    }
}
