using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Blazor.Pages.Forms.TravelExpenses;

/// <summary>The Bootstrap icon for each kind of trip module, shared by the builder and the overview.</summary>
public static class TripModuleIcons {
    public static string For(TripModuleDTO module) {
        return module.Kind switch {
            TripModuleKind.VehicleLeg => module.Vehicle == TripVehicle.Car ? "bi-car-front" : "bi-bicycle",
            TripModuleKind.TransitLeg => TransitIcon(module.TransitMode),
            TripModuleKind.HotelStay => "bi-building",
            TripModuleKind.ProvidedMeal => "bi-cup-hot",
            _ => "bi-receipt"
        };
    }

    private static string TransitIcon(TripTransitMode mode) {
        return mode switch {
            TripTransitMode.Plane => "bi-airplane",
            TripTransitMode.Bus => "bi-bus-front",
            _ => "bi-train-front"
        };
    }
}
