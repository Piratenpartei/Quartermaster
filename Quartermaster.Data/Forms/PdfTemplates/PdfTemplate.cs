using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Forms.PdfTemplates;

[Table(TableName, IsColumnAttributeRequired = false)]
public class PdfTemplate {
    public const string TableName = "PdfTemplates";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stable form key the template renders for; matches <c>FormCatalog.FormKey</c> (e.g. <c>expense-reimbursement</c>). Unique.</summary>
    public string Key { get; set; } = "";

    /// <summary>The editable template source: XML markup with embedded Fluid (see <c>PdfTemplateEngine</c>).</summary>
    public string Body { get; set; } = "";

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }
}
