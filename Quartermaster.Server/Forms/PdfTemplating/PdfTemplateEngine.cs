using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Fluid;
using Fluid.Values;
using Quartermaster.Server.Banking;

namespace Quartermaster.Server.Forms.PdfTemplating;

public class PdfTemplateEngine {
    private static readonly FluidParser Parser = new();

    // Escapes only the XML-significant characters in {{ }} outputs (so a "&" in a name can't break
    // the XML parse) while leaving Unicode - umlauts, the euro sign - untouched. Literal template
    // text is written verbatim, so the XML tags themselves are never escaped.
    private static readonly TextEncoder XmlValueEncoder = HtmlEncoder.Create(UnicodeRanges.All);

    private readonly BankCodeBicLookup _bicLookup;

    public PdfTemplateEngine(BankCodeBicLookup bicLookup) {
        _bicLookup = bicLookup;
    }

    public async Task<byte[]> RenderAsync(string templateBody, IDictionary<string, object> model, PdfTemplateAssetResolver assetResolver) {
        var xml = await RenderXmlAsync(templateBody, model);
        var tree = PdfTemplateXmlParser.Parse(xml);
        return PdfTemplateRenderer.Render(tree, assetResolver);
    }

    /// <summary>Exposed for tests / preview: renders Fluid to the intermediate XML without invoking QuestPDF.</summary>
    public async Task<string> RenderXmlAsync(string templateBody, IDictionary<string, object> model) {
        if (!Parser.TryParse(templateBody, out var parsed, out var error)) {
            throw new PdfTemplateParseException("Template Fluid parse error: " + error);
        }
        var options = new TemplateOptions();
        PdfTemplateFilters.Register(options, _bicLookup);
        options.ValueConverters.Add(value => value is Enum e ? new StringValue(e.ToString()) : null);
        var context = new TemplateContext(options);
        foreach (var (key, value) in model) {
            context.SetValue(key, value);
        }
        return await parsed.RenderAsync(context, XmlValueEncoder);
    }
}
