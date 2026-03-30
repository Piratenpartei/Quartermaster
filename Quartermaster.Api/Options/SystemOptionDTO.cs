using System;

namespace Quartermaster.Api.Options;

public class SystemOptionDTO {
    public string Identifier { get; set; } = "";
    public string Value { get; set; } = "";
    public Guid? ChapterId { get; set; }
}
