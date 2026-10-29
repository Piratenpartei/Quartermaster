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
    public const int ContactEmailMaxLength = 254;

    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Guid? ParentChapterId { get; set; }
    public string? ShortCode { get; set; }

    /// <summary>The value of the Keycloak attribute <c>ekklesia_department</c> that places a person in this chapter at login.</summary>
    public string? KeycloakDepartment { get; set; }

    /// <summary>Shown to signed-in users only; never put it into a <see cref="ChapterDTO"/> for anonymous callers.</summary>
    public string? ContactEmail { get; set; }
    public bool NoMemberAdministration { get; set; }
    public bool UnableToAct { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ChapterMark? MarkFor(ChapterMarkPurpose purpose) {
        if (UnableToAct)
            return ChapterMark.UnableToAct;
        if (NoMemberAdministration && purpose == ChapterMarkPurpose.MemberAdministration)
            return ChapterMark.NoMemberAdministration;
        return null;
    }

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
