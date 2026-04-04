using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Users;

[Table(TableName, IsColumnAttributeRequired = false)]
public class LoginAttempt {
    public const string TableName = "LoginAttempts";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string IpAddress { get; set; } = "";
    public string UsernameOrEmail { get; set; } = "";
    public bool Success { get; set; }
    public DateTime AttemptedAt { get; set; }
}
