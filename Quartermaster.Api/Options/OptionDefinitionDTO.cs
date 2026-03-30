using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Options;

public class OptionDefinitionDTO {
    public string Identifier { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public int DataType { get; set; }
    public bool IsOverridable { get; set; }
    public string TemplateModels { get; set; } = "";
    public string GlobalValue { get; set; } = "";
    public List<OptionOverrideDTO> Overrides { get; set; } = [];
}

public class OptionOverrideDTO {
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string ChapterShortCode { get; set; } = "";
    public string Value { get; set; } = "";
}
