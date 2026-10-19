using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Data.Forms.PdfTemplates;
using Quartermaster.Data.Receipts;
using Quartermaster.Server.Forms;
using Quartermaster.Server.Forms.PdfTemplating;

namespace Quartermaster.Server.TravelExpenses;

public class TravelExpensePdfService {
    public const string FormKey = TravelExpenseRulePresets.DefaultTemplateKey;

    private readonly PdfTemplateRepository _templates;
    private readonly PdfTemplateEngine _engine;
    private readonly FormAssets _assets;
    private readonly TravelExpenseService _service;
    private readonly UserReceiptRepository _receipts;
    private readonly ReceiptAppender _appender;
    private readonly I18nService _i18n;

    public TravelExpensePdfService(PdfTemplateRepository templates, PdfTemplateEngine engine, FormAssets assets, TravelExpenseService service,
        UserReceiptRepository receipts, ReceiptAppender appender, I18nService i18n) {
        _templates = templates;
        _engine = engine;
        _assets = assets;
        _service = service;
        _receipts = receipts;
        _appender = appender;
        _i18n = i18n;
    }

    /// <summary>The filled form; for a logged-in owner the referenced receipts follow, in the order the trip uses them.</summary>
    public async Task<byte[]> RenderAsync(TravelExpenseInputDTO input, Guid? ownerUserId) {
        var rules = _service.RulesFor(input);
        var calc = TravelExpenseCalculator.Calculate(input, rules, TravelExpenseService.Today());
        var receipts = ownerUserId == null ? [] : ReceiptsInOrder(input, ownerUserId.Value);

        var model = TravelExpensePdfModelBuilder.Build(input, rules, calc, receipts.Count, _i18n);
        var form = await _engine.RenderAsync(TemplateBody(rules), new Dictionary<string, object> { ["form"] = model }, _assets.Resolve);
        return _appender.Append(form, receipts);
    }

    private string TemplateBody(TravelExpenseRulesDTO rules) {
        var key = string.IsNullOrWhiteSpace(rules.TemplateKey) ? FormKey : rules.TemplateKey;
        return _templates.GetByKey(key)?.Body
            ?? _templates.GetByKey(FormKey)?.Body
            ?? PdfTemplateDefaults.ByKey[FormKey];
    }

    private List<UserReceipt> ReceiptsInOrder(TravelExpenseInputDTO input, Guid ownerUserId) {
        var ids = input.Modules
            .SelectMany(m => m.ReceiptIds)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return [];

        var owned = _receipts.GetOwned(ids, ownerUserId).ToDictionary(r => r.Id);
        return ids
            .Where(owned.ContainsKey)
            .Select(id => owned[id])
            .ToList();
    }
}
