using System.Threading.Tasks;
using Fluid;
using Fluid.Ast;

namespace Quartermaster.Rendering;

public record EnvelopeData(
    string SenderName,
    string SenderStreet,
    string SenderPostcode,
    string SenderCity,
    string SenderCountry,
    string RecipientName,
    string RecipientStreet,
    string RecipientPostcode,
    string RecipientCity,
    string RecipientCountry);

public static class EnvelopeTags {
    private const string KeyPrefix = "__envelope_";

    private static readonly (string TagName, string ContextKey)[] Tags = new[] {
        ("envelope_sender_name", "sender_name"),
        ("envelope_sender_street", "sender_street"),
        ("envelope_sender_postcode", "sender_postcode"),
        ("envelope_sender_city", "sender_city"),
        ("envelope_sender_country", "sender_country"),
        ("envelope_recipient_name", "recipient_name"),
        ("envelope_recipient_street", "recipient_street"),
        ("envelope_recipient_postcode", "recipient_postcode"),
        ("envelope_recipient_city", "recipient_city"),
        ("envelope_recipient_country", "recipient_country")
    };

    public static void Register(FluidParser parser) {
        foreach (var (tagName, contextKey) in Tags) {
            var fullKey = KeyPrefix + contextKey;
            parser.RegisterExpressionTag(tagName, async (expression, writer, encoder, context) => {
                var value = await expression.EvaluateAsync(context);
                context.AmbientValues[fullKey] = value.ToStringValue();
                return Completion.Normal;
            });
        }
    }

    public static EnvelopeData Extract(TemplateContext context) {
        return new EnvelopeData(
            Read(context, "sender_name"),
            Read(context, "sender_street"),
            Read(context, "sender_postcode"),
            Read(context, "sender_city"),
            Read(context, "sender_country"),
            Read(context, "recipient_name"),
            Read(context, "recipient_street"),
            Read(context, "recipient_postcode"),
            Read(context, "recipient_city"),
            Read(context, "recipient_country"));
    }

    private static string Read(TemplateContext context, string suffix) {
        if (context.AmbientValues.TryGetValue(KeyPrefix + suffix, out var v) && v is string s)
            return s;
        return "";
    }

    public static string EmptyTagsSnippet => string.Join("\n", new[] {
        "{%- comment -%} Pass Fluid expressions (not strings). Use filters like | append to concatenate. {%- endcomment -%}",
        "{%- envelope_sender_name \"\" -%}",
        "{%- envelope_sender_street \"\" -%}",
        "{%- envelope_sender_postcode \"\" -%}",
        "{%- envelope_sender_city \"\" -%}",
        "{%- envelope_sender_country \"\" -%}",
        "{%- envelope_recipient_name \"\" -%}",
        "{%- envelope_recipient_street \"\" -%}",
        "{%- envelope_recipient_postcode \"\" -%}",
        "{%- envelope_recipient_city \"\" -%}",
        "{%- envelope_recipient_country \"\" -%}",
        "",
        ""
    });
}
