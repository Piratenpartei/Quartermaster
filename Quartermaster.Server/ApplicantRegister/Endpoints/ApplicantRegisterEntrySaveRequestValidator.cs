using System;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Api.I18n;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.ApplicantRegister.Endpoints;

public class ApplicantRegisterEntrySaveRequestValidator : Validator<ApplicantRegisterEntrySaveRequest> {
    public ApplicantRegisterEntrySaveRequestValidator() {
        RuleFor(x => x)
            .Must(HasNames)
            .WithMessage(I18nKey.Error.ApplicantRegister.NamesRequired);

        RuleFor(x => x.Kind)
            .IsInEnum()
            .WithMessage(I18nKey.Error.ApplicantRegister.KindInvalid);

        RuleFor(x => x)
            .Must(HasIdentifyingDetail)
            .WithMessage(I18nKey.Error.ApplicantRegister.IdentifyingDetailRequired);

        RuleFor(x => x)
            .Must(FieldsFit)
            .WithMessage(I18nKey.Error.ApplicantRegister.FieldTooLong);

        RuleFor(x => x.ChapterId)
            .Must(chapterId => chapterId == null || Resolve<ChapterRepository>().Exists(chapterId.Value))
            .WithMessage(I18nKey.Error.ApplicantRegister.ChapterUnknown);

        RuleFor(x => x.Email)
            .Must(email => string.IsNullOrWhiteSpace(email) || EmailAddressFormat.IsPlausible(email))
            .WithMessage(I18nKey.Error.ApplicantRegister.EmailInvalid);

        RuleFor(x => x.OccurredOn)
            .Must(date => date != default)
            .WithMessage(I18nKey.Error.ApplicantRegister.OccurredOnRequired);

        RuleFor(x => x.OccurredOn)
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage(I18nKey.Error.ApplicantRegister.OccurredOnInFuture);
    }

    private static bool HasNames(ApplicantRegisterEntrySaveRequest request)
        => !string.IsNullOrWhiteSpace(request.FirstName) && !string.IsNullOrWhiteSpace(request.LastName);

    private static bool HasIdentifyingDetail(ApplicantRegisterEntrySaveRequest request) {
        if (request.DateOfBirth.HasValue)
            return true;
        if (!string.IsNullOrWhiteSpace(request.Email))
            return true;
        if (!string.IsNullOrWhiteSpace(request.PostCode) && !string.IsNullOrWhiteSpace(request.City))
            return true;
        return !string.IsNullOrWhiteSpace(request.Street) && !string.IsNullOrWhiteSpace(request.HouseNumber);
    }

    private static bool FieldsFit(ApplicantRegisterEntrySaveRequest request) {
        if (TrimmedLength(request.FirstName) > ApplicantRegisterEntry.NameMaxLength || TrimmedLength(request.LastName) > ApplicantRegisterEntry.NameMaxLength)
            return false;
        if (TrimmedLength(request.Street) > ApplicantRegisterEntry.StreetMaxLength || TrimmedLength(request.HouseNumber) > ApplicantRegisterEntry.HouseNumberMaxLength)
            return false;
        if (TrimmedLength(request.PostCode) > ApplicantRegisterEntry.PostCodeMaxLength || TrimmedLength(request.City) > ApplicantRegisterEntry.CityMaxLength)
            return false;
        if (TrimmedLength(request.Email) > ApplicantRegisterEntry.EmailMaxLength)
            return false;
        return TrimmedLength(request.Note) <= ApplicantRegisterEntry.NoteMaxLength;
    }

    private static int TrimmedLength(string? value) {
        return value?.Trim().Length ?? 0;
    }
}
