namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionConnectionCheckDTO {
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
}

public class AssemblyMotionWikiResyncAllDTO {
    public int Queued { get; set; }
}
