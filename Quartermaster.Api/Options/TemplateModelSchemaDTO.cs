using System.Collections.Generic;

namespace Quartermaster.Api.Options;

public class TemplateModelSchemaDTO {
    public string ModelName { get; set; } = "";
    public string VariablePrefix { get; set; } = "";
    public List<TemplateFieldDTO> Fields { get; set; } = [];
}

public class TemplateFieldDTO {
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string FluidExpression { get; set; } = "";
}
