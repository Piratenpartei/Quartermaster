using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Config;

public class ClientConfigDTO {
    public string ErrorContact { get; set; } = "";
    public bool ShowDetailedErrors { get; set; }
    public bool SamlEnabled { get; set; }
    public string SamlButtonText { get; set; } = "";
    public string SsoSupportContact { get; set; } = "";
    public bool OidcEnabled { get; set; }
    public string OidcButtonText { get; set; } = "";
    public bool IsDebug { get; set; }
    public bool MembershipApplicationEnabled { get; set; }
    public bool DueSelectionEnabled { get; set; }
    public bool PermanentReductionEnabled { get; set; }

    /// <summary>The highest yearly amount a reduction can ask for: the regular minimum, from which no decision is needed.</summary>
    public decimal ReducedDueMaximum { get; set; }
    public List<Guid> MotionFormChapterIds { get; set; } = [];
}
