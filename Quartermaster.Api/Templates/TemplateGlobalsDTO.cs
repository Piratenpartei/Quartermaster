using System;

namespace Quartermaster.Api.Templates;

public class TemplateGlobalsDTO {
    public string BaseUrl { get; set; } = "";
    public string AppName { get; set; } = "";
    public DateTime Now { get; set; }
}
