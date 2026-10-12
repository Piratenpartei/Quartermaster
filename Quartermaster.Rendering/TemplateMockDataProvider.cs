using System;
using System.Collections.Generic;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Events;
using Quartermaster.Api.Members;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Api.Templates;

namespace Quartermaster.Rendering;

public static class TemplateMockDataProvider {
    public static Dictionary<string, object> GetMockData(string templateModels) {
        var data = new Dictionary<string, object> {
            ["globals"] = new TemplateGlobalsDTO {
                BaseUrl = "https://quartermaster.example.local",
                AppName = "Quartermaster",
                Now = DateTime.UtcNow
            }
        };
        var models = templateModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var model in models) {
            switch (model) {
                case "TemplateConfirmationDTO":
                    data["confirm"] = new TemplateConfirmationDTO {
                        Url = "https://quartermaster.example.local/Confirm/abc123"
                    };
                    break;

                case "ChapterDTO":
                    data["chapter"] = new ChapterDTO {
                        Id = Guid.NewGuid(),
                        Name = "Piratenpartei Berlin"
                    };
                    break;

                case "MembershipApplicationDetailDTO":
                    data["application"] = new MembershipApplicationDetailDTO {
                        Id = Guid.NewGuid(),
                        FirstName = "Max",
                        LastName = "Mustermann",
                        DateOfBirth = new DateOnly(1990, 1, 15),
                        Citizenship = "Deutsch",
                        Email = "max.mustermann@example.com",
                        PhoneNumber = "0170 1234567",
                        AddressStreet = "Musterstraße",
                        AddressHouseNbr = "42",
                        AddressPostCode = "10115",
                        AddressCity = "Berlin",
                        ChapterName = "Piratenpartei Berlin",
                        Status = ApplicationStatus.Approved,
                        SubmittedAt = DateTimeOffset.UtcNow.AddDays(-3),
                        EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                        HasReducedDueSelection = true
                    };
                    break;

                case "DueSelectionDetailDTO":
                    data["selection"] = new DueSelectionDetailDTO {
                        Id = Guid.NewGuid(),
                        FirstName = "Max",
                        LastName = "Mustermann",
                        Email = "max.mustermann@example.com",
                        SelectedValuation = SelectedValuation.Reduced,
                        SelectedDue = 24,
                        ReducedAmount = 24,
                        ReducedJustification = "Student ohne Einkommen",
                        Status = DueSelectionStatus.Approved
                    };
                    break;

                case "MotionDetailDTO":
                    data["motion"] = new MotionDetailDTO {
                        Id = Guid.NewGuid(),
                        ChapterId = Guid.NewGuid(),
                        ChapterName = "Piratenpartei Berlin",
                        AuthorName = "Erika Musterfrau",
                        AuthorEmail = "erika.musterfrau@example.com",
                        Title = "Beispielantrag: Änderung der Geschäftsordnung",
                        Text = "<p>Beispieltext für einen Antrag.</p>",
                        TextMarkdown = "Beispieltext für einen Antrag.",
                        IsPublic = true,
                        ApprovalStatus = MotionApprovalStatus.Pending,
                        CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
                    };
                    break;

                case "AssemblyMotionNotificationModel":
                    data["motion"] = new AssemblyMotionNotificationModel {
                        Id = Guid.NewGuid(),
                        DisplayNumber = "A001",
                        Title = "Beispielantrag: Änderung der Geschäftsordnung",
                        TitleEscaped = "Beispielantrag: Änderung der Geschäftsordnung",
                        TypeName = "Satzungsänderungsantrag",
                        EventName = "Mitgliederversammlung Q3",
                        StatusLabel = "Eingereicht",
                        CheckNote = "Beispielhinweis der Antragskommission.",
                        ChangeNote = "Tippfehler im Antragstext korrigiert.",
                        InquiryQuestion = "Beispielfrage der Antragskommission.",
                        InquiryDueAt = "20.10. um 18:00 Uhr",
                        Deadline = "25.10. um 23:59 Uhr",
                        Path = "/AssemblyMotions/00000000-0000-0000-0000-000000000000",
                        OtherDisplayNumber = "A002",
                        OtherTitle = "Beispielantrag eines anderen Teams",
                        OtherTitleEscaped = "Beispielantrag eines anderen Teams"
                    };
                    break;

                case "MemberDetailDTO":
                    data["member"] = new MemberDetailDTO {
                        Id = Guid.NewGuid(),
                        MemberNumber = 12345,
                        FirstName = "Max",
                        LastName = "Mustermann",
                        Email = "max.mustermann@example.com",
                        PostCode = "10115",
                        City = "Berlin",
                        Street = "Musterstraße 42",
                        Country = "DE",
                        DateOfBirth = new DateOnly(1990, 1, 15),
                        Citizenship = "DE",
                        ChapterName = "Piratenpartei Berlin",
                        MembershipFee = 72m,
                        EntryDate = new DateOnly(2020, 3, 1),
                        HasVotingRights = true,
                        LastImportedAt = DateTimeOffset.UtcNow
                    };
                    break;

                case "EventDetailDTO":
                    data["event"] = new EventDetailDTO {
                        Id = Guid.NewGuid(),
                        ChapterId = Guid.NewGuid(),
                        ChapterName = "Piratenpartei Berlin",
                        InternalName = "Mitgliederversammlung Q3",
                        PublicName = "Mitgliederversammlung",
                        Description = "Reguläre Mitgliederversammlung im dritten Quartal.",
                        EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
                        CreatedAt = DateTimeOffset.UtcNow.AddDays(-7)
                    };
                    break;
            }
        }

        return data;
    }
}
