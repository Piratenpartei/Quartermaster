using System;

namespace Quartermaster.Api.TravelExpenses;

/// <summary>A published rule set a chapter's treasurer can start from.</summary>
public record TravelExpenseRulePreset(string Key, string Name, Func<TravelExpenseRulesDTO> Create);
