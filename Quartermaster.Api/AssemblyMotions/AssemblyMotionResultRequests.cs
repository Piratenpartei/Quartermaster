using System;
using System.Collections.Generic;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionResultRequest {
    public Guid Id { get; set; }
    public AssemblyMotionResult Result { get; set; }
}

public class AssemblyMotionResultItem {
    public Guid MotionId { get; set; }
    public AssemblyMotionResult Result { get; set; }
}

public class AssemblyMotionBulkResultRequest {
    public Guid PortalId { get; set; }
    public List<AssemblyMotionResultItem> Items { get; set; } = [];
}
