using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.PublicForms;

namespace Quartermaster.Server.MembershipApplications;

public class ApplicationReduction {
    private readonly DueSelectionRepository _dueSelections;
    private readonly PublicFormSettings _publicForms;

    public ApplicationReduction(DueSelectionRepository dueSelections, PublicFormSettings publicForms) {
        _dueSelections = dueSelections;
        _publicForms = publicForms;
    }

    /// <summary>True when the application's dues need their own decision (a reduction below the regular minimum).</summary>
    public bool IsReduced(MembershipApplication application) {
        if (application.DueSelectionId == null)
            return false;
        var dueSelection = _dueSelections.Get(application.DueSelectionId.Value);
        return dueSelection != null && DueAmount.NeedsDecision(dueSelection.ToDto(), _publicForms.ReducedDueMaximum());
    }
}
