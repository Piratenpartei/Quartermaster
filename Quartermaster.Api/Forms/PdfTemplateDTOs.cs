using System;

namespace Quartermaster.Api.Forms;

public class PdfTemplateListItemDTO {
    public string Key { get; set; } = "";
    public string DisplayNameI18nKey { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

public class PdfTemplateDTO {
    public string Key { get; set; } = "";
    public string DisplayNameI18nKey { get; set; } = "";
    public string Body { get; set; } = "";
    public string PaletteModels { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

public class PdfTemplateSaveRequest {
    public string Body { get; set; } = "";
}

public class PdfTemplatePreviewRequest {
    public string Body { get; set; } = "";
}
