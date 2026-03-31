using System;
using System.Collections.Generic;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Members;
using Quartermaster.Api.MembershipApplications;

namespace Quartermaster.Api.Rendering;

public static class TemplateMockDataProvider {
    public static Dictionary<string, object> GetMockData(string templateModels) {
        var data = new Dictionary<string, object>();
        var models = templateModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var model in models) {
            switch (model) {
                case "MembershipApplicationDetailDTO":
                    data["application"] = new MembershipApplicationDetailDTO {
                        Id = Guid.NewGuid(),
                        FirstName = "Max",
                        LastName = "Mustermann",
                        DateOfBirth = new DateTime(1990, 1, 15),
                        Citizenship = "Deutsch",
                        EMail = "max.mustermann@example.com",
                        PhoneNumber = "0170 1234567",
                        AddressStreet = "Musterstraße",
                        AddressHouseNbr = "42",
                        AddressPostCode = "10115",
                        AddressCity = "Berlin",
                        ChapterName = "Piratenpartei Berlin",
                        Status = 1,
                        SubmittedAt = DateTime.UtcNow.AddDays(-3),
                        EntryDate = DateTime.UtcNow
                    };
                    break;

                case "DueSelectionDetailDTO":
                    data["selection"] = new DueSelectionDetailDTO {
                        Id = Guid.NewGuid(),
                        FirstName = "Max",
                        LastName = "Mustermann",
                        EMail = "max.mustermann@example.com",
                        SelectedValuation = 4,
                        SelectedDue = 24,
                        ReducedAmount = 24,
                        ReducedJustification = "Student ohne Einkommen",
                        Status = 1
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
                        EMail = "max.mustermann@example.com",
                        PostCode = "10115",
                        City = "Berlin",
                        Street = "Musterstraße 42",
                        Country = "DE",
                        DateOfBirth = new DateTime(1990, 1, 15),
                        Citizenship = "DE",
                        ChapterName = "Piratenpartei Berlin",
                        MembershipFee = 72m,
                        EntryDate = new DateTime(2020, 3, 1),
                        HasVotingRights = true,
                        LastImportedAt = DateTime.UtcNow
                    };
                    break;
            }
        }

        return data;
    }
}
