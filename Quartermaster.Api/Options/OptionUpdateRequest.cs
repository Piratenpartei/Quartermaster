using System;

namespace Quartermaster.Api.Options;

public class OptionUpdateRequest {
    public string Identifier { get; set; } = "";
    public Guid? ChapterId { get; set; }
    public string Value { get; set; } = "";
}
