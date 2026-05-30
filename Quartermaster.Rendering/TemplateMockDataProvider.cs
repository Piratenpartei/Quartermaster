using System;
using System.Collections.Generic;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Members;
using Quartermaster.Api.MembershipApplications;

namespace Quartermaster.Rendering;

public static class TemplateMockDataProvider {
    public static Dictionary<string, object> GetMockData(string templateModels) {
        var data = new Dictionary<string, object> {
            ["globals"] = new Dictionary<string, object?> {
                ["base_url"] = "https://quartermaster.example.local",
                ["app_name"] = "Quartermaster",
                ["now"] = DateTime.UtcNow
            }
        };
        var models = templateModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var model in models) {
            switch (model) {
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
                        EntryDate = DateOnly.FromDateTime(DateTime.UtcNow)
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

                case "ChapterDTO":
                    data["chapter"] = new ChapterDTO {
                        Id = Guid.NewGuid(),
                        Name = "Piratenpartei Berlin"
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

                case "MotionSubmittedPayload":
                    data["motion"] = new {
                        Id = Guid.NewGuid(),
                        Title = "Beispielantrag: Änderung der Geschäftsordnung",
                        AuthorName = "Erika Musterfrau",
                        CreatedAt = DateTime.UtcNow
                    };
                    data["chapter"] = new {
                        Id = Guid.NewGuid(),
                        Name = "Piratenpartei Berlin"
                    };
                    break;

                case "ApplicationSubmittedPayload":
                    data["application"] = new {
                        Id = Guid.NewGuid(),
                        FirstName = "Max",
                        LastName = "Mustermann",
                        Email = "max.mustermann@example.com",
                        SubmittedAt = DateTime.UtcNow,
                        HasReducedDueSelection = true
                    };
                    data["chapter"] = new {
                        Id = Guid.NewGuid(),
                        Name = "Piratenpartei Berlin"
                    };
                    break;

                case "DueSelectionSubmittedPayload":
                    data["selection"] = new {
                        Id = Guid.NewGuid(),
                        FirstName = "Max",
                        LastName = "Mustermann",
                        Email = "max.mustermann@example.com",
                        SelectedDue = 12m,
                        ReducedAmount = 12m,
                        ReducedJustification = "Studierender ohne Einkommen",
                        SubmittedAt = DateTime.UtcNow
                    };
                    data["chapter"] = new {
                        Id = Guid.NewGuid(),
                        Name = "Piratenpartei Berlin"
                    };
                    break;
            }
        }

        return data;
    }
}
