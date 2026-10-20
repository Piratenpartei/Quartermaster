using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Forwarding;

[Table(TableName, IsColumnAttributeRequired = false)]
public class RedmineConnection {
    public const string TableName = "RedmineConnections";
    public const int BaseUrlMaxLength = 500;
    public const int ApiKeyMaxLength = 512;

    [PrimaryKey]
    public Guid ChapterId { get; set; }

    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}
