using Quartermaster.Api.DueSelector;

namespace Quartermaster.Server.Forwarding;

public static class DueAmount {
    /// <summary>The yearly amount the member will pay: the requested amount of a reduction, otherwise the selected due.</summary>
    public static decimal Effective(DueSelectionDTO dto) {
        return dto.SelectedValuation == SelectedValuation.Reduced ? dto.ReducedAmount : dto.SelectedDue;
    }

    /// <summary>Only a reduction below the regular minimum needs a decision; the statutory youth rate does not.</summary>
    public static bool NeedsDecision(DueSelectionDTO dto, decimal regularMinimum) {
        return dto.SelectedValuation == SelectedValuation.Reduced && Effective(dto) < regularMinimum;
    }
}
