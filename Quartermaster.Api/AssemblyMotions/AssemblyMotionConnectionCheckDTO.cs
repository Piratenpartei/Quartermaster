using System.Collections.Generic;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionConnectionCheckDTO {
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
    public List<string> Warnings { get; set; } = [];
}

public class AssemblyMotionWikiResyncAllDTO {
    public int Queued { get; set; }
}

public class AssemblyMotionForumResyncAllDTO {
    public int Queued { get; set; }
}
