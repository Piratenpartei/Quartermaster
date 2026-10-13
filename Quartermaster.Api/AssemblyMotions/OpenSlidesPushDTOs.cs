using System;
using System.Collections.Generic;

namespace Quartermaster.Api.AssemblyMotions;

public class OpenSlidesPushRequest {
    public Guid PortalId { get; set; }
    public bool IncludeSubmitted { get; set; }
}

public class OpenSlidesPushResultDTO {
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public List<OpenSlidesPushFailureDTO> Failures { get; set; } = [];
}

public class OpenSlidesPushFailureDTO {
    public string DisplayNumber { get; set; } = "";
    public string Error { get; set; } = "";
}
