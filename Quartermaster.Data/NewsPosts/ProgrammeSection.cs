using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.NewsPosts;

[Table(TableName, IsColumnAttributeRequired = false)]
public class ProgrammeSection {
    public const string TableName = "ProgrammeSections";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceId { get; set; }

    // Needed because a prefix source spans many pages; the section link is
    // https://wiki.piratenpartei.de/{PageTitle}#{Anchor} with spaces as underscores.
    public string PageTitle { get; set; } = "";
    public string Heading { get; set; } = "";
    public string HeadingPath { get; set; } = "";
    public string Anchor { get; set; } = "";
    public string Body { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public byte[] Embedding { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}
