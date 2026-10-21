using System;

namespace Quartermaster.Api.SupportSignatures;

public class SignatureIntakeSaveRequest {
    /// <summary>The campaign on create, the intake on update; bound from the route.</summary>
    public Guid Id { get; set; }
    public Guid AdministrativeDivisionId { get; set; }
    public int Count { get; set; }
    public bool AlreadyCertified { get; set; }
    public DateOnly ReceivedOn { get; set; }
    public string? Note { get; set; }
}

public class SignatureIntakeIdRequest {
    public Guid Id { get; set; }
}

public class SignatureIntakeDTO {
    public Guid Id { get; set; }
    public Guid AdministrativeDivisionId { get; set; }
    public string MunicipalityName { get; set; } = "";
    public int Count { get; set; }
    public bool AlreadyCertified { get; set; }
    public DateOnly ReceivedOn { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
