using System;
using System.Collections.Generic;
using LinqToDB.Mapping;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;

namespace Quartermaster.Data.Chapters;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Chapter {
    public const string TableName = "Chapters";
    public const int KeycloakDepartmentMaxLength = 200;

    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Guid? ParentChapterId { get; set; }
    public string? ShortCode { get; set; }

    /// <summary>The value of the Keycloak attribute <c>ekklesia_department</c> that places a person in this chapter at login.</summary>
    public string? KeycloakDepartment { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ChapterDTO ToDto(IReadOnlyList<string> externalCodes, IReadOnlyList<Guid> administrativeDivisionIds) => new() {
        Id = Id,
        Name = Name,
        ShortCode = ShortCode,
        KeycloakDepartment = KeycloakDepartment,
        ParentChapterId = ParentChapterId,
        ExternalCodes = [.. externalCodes],
        AdministrativeDivisionIds = [.. administrativeDivisionIds],
        ArchivedAt = ArchivedAt.ToDtoUtc()
    };

    /// <summary>For callers that only need the scalar fields (notification template models etc.); codes/divisions come back empty.</summary>
    public ChapterDTO ToDto() => ToDto([], []);
}
