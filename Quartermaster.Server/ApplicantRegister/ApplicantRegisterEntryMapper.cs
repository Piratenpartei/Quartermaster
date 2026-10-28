using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.ApplicantRegister;

public class ApplicantRegisterEntryMapper {
    private readonly ChapterRepository _chapters;

    public ApplicantRegisterEntryMapper(ChapterRepository chapters) {
        _chapters = chapters;
    }

    public ApplicantRegisterEntryDTO ToDto(ApplicantRegisterEntry entry) {
        var chapterIds = entry.ChapterId == null ? [] : new List<Guid> { entry.ChapterId.Value };
        return ToDto(entry, _chapters.GetNames(chapterIds));
    }

    public List<ApplicantRegisterEntryDTO> ToDtos(IReadOnlyList<ApplicantRegisterEntry> entries) {
        var chapterIds = entries
            .Where(e => e.ChapterId != null)
            .Select(e => e.ChapterId!.Value)
            .Distinct()
            .ToList();
        var chapterNames = _chapters.GetNames(chapterIds);
        return entries.Select(e => ToDto(e, chapterNames)).ToList();
    }

    private static ApplicantRegisterEntryDTO ToDto(ApplicantRegisterEntry entry, Dictionary<Guid, string> chapterNames) {
        return new ApplicantRegisterEntryDTO {
            Id = entry.Id,
            Kind = entry.Kind,
            FirstName = entry.FirstName,
            LastName = entry.LastName,
            DateOfBirth = entry.DateOfBirth.HasValue ? DateOnly.FromDateTime(entry.DateOfBirth.Value) : null,
            Street = entry.Street,
            HouseNumber = entry.HouseNumber,
            PostCode = entry.PostCode,
            City = entry.City,
            Email = entry.Email,
            OccurredOn = DateOnly.FromDateTime(entry.OccurredOn),
            Note = entry.Note,
            ChapterId = entry.ChapterId,
            ChapterName = entry.ChapterId != null && chapterNames.TryGetValue(entry.ChapterId.Value, out var chapterName) ? chapterName : "",
            SourceApplicationId = entry.SourceApplicationId,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(entry.CreatedAt, DateTimeKind.Utc))
        };
    }

    /// <summary>Copies the editable fields only; origin, creation time and creator stay as stored.</summary>
    public static void ApplyEditableFields(ApplicantRegisterEntry entry, ApplicantRegisterEntrySaveRequest request) {
        entry.Kind = request.Kind;
        entry.FirstName = request.FirstName.Trim();
        entry.LastName = request.LastName.Trim();
        entry.DateOfBirth = request.DateOfBirth?.ToDateTime(TimeOnly.MinValue);
        entry.Street = request.Street?.Trim() ?? "";
        entry.HouseNumber = request.HouseNumber?.Trim() ?? "";
        entry.PostCode = request.PostCode?.Trim() ?? "";
        entry.City = request.City?.Trim() ?? "";
        entry.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        entry.OccurredOn = request.OccurredOn.ToDateTime(TimeOnly.MinValue);
        entry.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        entry.ChapterId = request.ChapterId;
    }
}
