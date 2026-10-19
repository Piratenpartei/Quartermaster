using System;

namespace Quartermaster.Api.TravelExpenses;

/// <summary>
/// Where a module sits in the trip timeline. Provided meals and other costs only carry a day, so they are placed at the
/// usual meal time or at the end of that day instead of at midnight before the departure.
/// </summary>
public static class TripModuleOrder {
    public static DateTime SortKey(TripModuleDTO module) {
        if (module.At == null)
            return DateTime.MaxValue;

        var day = module.At.Value.Date;
        return module.Kind switch {
            TripModuleKind.ProvidedMeal => day + MealTime(module.Meal),
            TripModuleKind.OtherCost when module.At.Value.TimeOfDay == TimeSpan.Zero => day.AddDays(1).AddTicks(-1),
            _ => module.At.Value
        };
    }

    private static TimeSpan MealTime(TripMeal meal) {
        return meal switch {
            TripMeal.Breakfast => new TimeSpan(8, 0, 0),
            TripMeal.Lunch => new TimeSpan(12, 30, 0),
            _ => new TimeSpan(19, 0, 0)
        };
    }
}
