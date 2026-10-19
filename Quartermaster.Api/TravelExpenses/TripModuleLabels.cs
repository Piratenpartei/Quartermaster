using Quartermaster.Api.I18n;

namespace Quartermaster.Api.TravelExpenses;

/// <summary>The i18n keys naming trip modules and meal days, shared by the wizard and the PDF.</summary>
public static class TripModuleLabels {
    public static string TitleKey(TripModuleDTO module) {
        return module.Kind switch {
            TripModuleKind.VehicleLeg => module.Vehicle == TripVehicle.Car ? I18nKey.Ui.TravelExpense.Module.VehicleCar : I18nKey.Ui.TravelExpense.Module.VehicleMotorcycle,
            TripModuleKind.TransitLeg => module.TransitMode switch {
                TripTransitMode.Train => I18nKey.Ui.TravelExpense.Module.TransitTrain,
                TripTransitMode.Bus => I18nKey.Ui.TravelExpense.Module.TransitBus,
                TripTransitMode.Plane => I18nKey.Ui.TravelExpense.Module.TransitPlane,
                _ => I18nKey.Ui.TravelExpense.Module.TransitOther
            },
            TripModuleKind.HotelStay => I18nKey.Ui.TravelExpense.Module.HotelStay,
            TripModuleKind.ProvidedMeal => module.Meal switch {
                TripMeal.Breakfast => I18nKey.Ui.TravelExpense.Module.MealBreakfast,
                TripMeal.Lunch => I18nKey.Ui.TravelExpense.Module.MealLunch,
                _ => I18nKey.Ui.TravelExpense.Module.MealDinner
            },
            _ => I18nKey.Ui.TravelExpense.Module.OtherCost
        };
    }

    public static string MealDayKey(MealDayKind kind) {
        return kind switch {
            MealDayKind.DayTrip => I18nKey.Ui.TravelExpense.MealDay.DayTrip,
            MealDayKind.ArrivalDeparture => I18nKey.Ui.TravelExpense.MealDay.ArrivalDeparture,
            MealDayKind.FullDay => I18nKey.Ui.TravelExpense.MealDay.FullDay,
            _ => I18nKey.Ui.TravelExpense.MealDay.None
        };
    }

    public static string NightsKey(int nights) {
        return nights == 1 ? I18nKey.Ui.TravelExpense.Pdf.OneNight : I18nKey.Ui.TravelExpense.Pdf.Nights;
    }
}
