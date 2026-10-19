using System;
using System.Collections.Generic;

namespace Quartermaster.Api.TravelExpenses;

/// <summary>Where the caller may edit travel expense rules.</summary>
public class TravelExpenseRuleScopesDTO {
    /// <summary>The global default, which applies to every chapter without rules of its own.</summary>
    public bool CanEditGlobal { get; set; }

    /// <summary>The chapters, including inheriting sub-chapters; null means every chapter.</summary>
    public List<Guid>? ChapterIds { get; set; } = [];
}

public class TravelExpenseRulePresetDTO {
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public TravelExpenseRulesDTO Rules { get; set; } = new();
}
