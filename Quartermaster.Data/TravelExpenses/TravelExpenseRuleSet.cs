using System;
using System.Collections.Generic;
using System.Text.Json;
using LinqToDB.Mapping;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Data.TravelExpenses;

[Table(TableName, IsColumnAttributeRequired = false)]
public class TravelExpenseRuleSet {
    public const string TableName = "TravelExpenseRuleSets";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Null for the global default.</summary>
    public Guid? ChapterId { get; set; }
    public DateTime ValidFrom { get; set; }
    public string VersionLabel { get; set; } = "";
    public string HeaderLines { get; set; } = "";
    public string RulesText { get; set; } = "";
    public string TemplateKey { get; set; } = "";

    /// <summary>The car rate steps as JSON; always read and written together with the rule set.</summary>
    public string CarRatesJson { get; set; } = "[]";
    public string MotorcycleRatesJson { get; set; } = "[]";

    public decimal MealDayTripRate { get; set; }
    public decimal MealArrivalDepartureRate { get; set; }
    public decimal MealFullDayRate { get; set; }
    public decimal BreakfastReductionPercent { get; set; }
    public decimal LunchReductionPercent { get; set; }
    public decimal DinnerReductionPercent { get; set; }
    public decimal AccommodationFlatRate { get; set; }
    public int SubmissionDeadlineDays { get; set; }
    public int CutoffDay { get; set; }
    public int CutoffMonth { get; set; }

    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public TravelExpenseRulesDTO ToDto() {
        return new TravelExpenseRulesDTO {
            Id = Id,
            ChapterId = ChapterId,
            ValidFrom = DateOnly.FromDateTime(ValidFrom),
            VersionLabel = VersionLabel,
            HeaderLines = HeaderLines,
            RulesText = RulesText,
            TemplateKey = TemplateKey,
            CarRates = ReadRates(CarRatesJson),
            MotorcycleRates = ReadRates(MotorcycleRatesJson),
            MealDayTripRate = MealDayTripRate,
            MealArrivalDepartureRate = MealArrivalDepartureRate,
            MealFullDayRate = MealFullDayRate,
            BreakfastReductionPercent = BreakfastReductionPercent,
            LunchReductionPercent = LunchReductionPercent,
            DinnerReductionPercent = DinnerReductionPercent,
            AccommodationFlatRate = AccommodationFlatRate,
            SubmissionDeadlineDays = SubmissionDeadlineDays,
            CutoffDay = CutoffDay,
            CutoffMonth = CutoffMonth
        };
    }

    /// <summary>Copies every editable field from the DTO; id and chapter stay as they are.</summary>
    public void Apply(TravelExpenseRulesDTO dto) {
        ValidFrom = dto.ValidFrom.ToDateTime(TimeOnly.MinValue);
        VersionLabel = dto.VersionLabel;
        HeaderLines = dto.HeaderLines;
        RulesText = dto.RulesText;
        TemplateKey = dto.TemplateKey;
        CarRatesJson = JsonSerializer.Serialize(dto.CarRates);
        MotorcycleRatesJson = JsonSerializer.Serialize(dto.MotorcycleRates);
        MealDayTripRate = dto.MealDayTripRate;
        MealArrivalDepartureRate = dto.MealArrivalDepartureRate;
        MealFullDayRate = dto.MealFullDayRate;
        BreakfastReductionPercent = dto.BreakfastReductionPercent;
        LunchReductionPercent = dto.LunchReductionPercent;
        DinnerReductionPercent = dto.DinnerReductionPercent;
        AccommodationFlatRate = dto.AccommodationFlatRate;
        SubmissionDeadlineDays = dto.SubmissionDeadlineDays;
        CutoffDay = dto.CutoffDay;
        CutoffMonth = dto.CutoffMonth;
    }

    private static List<RateStepDTO> ReadRates(string json) {
        return string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<RateStepDTO>>(json) ?? [];
    }
}
