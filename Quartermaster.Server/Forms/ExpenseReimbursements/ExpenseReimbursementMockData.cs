using System;
using System.Collections.Generic;
using Quartermaster.Api.Forms.ExpenseReimbursements;
using Quartermaster.Server.Forms.PdfTemplating;

namespace Quartermaster.Server.Forms.ExpenseReimbursements;

public class ExpenseReimbursementMockData : IFormMockData {
    public string FormKey => ExpenseReimbursementPdfService.FormKey;

    public IDictionary<string, object> BuildModel() {
        var sample = new ExpenseReimbursementInputDTO {
            FirstName = "Max",
            LastName = "Mustermann",
            MemberNumber = 12345,
            Street = "Beispielstraße 1",
            PostCode = "10115",
            City = "Berlin",
            ChapterName = "Piraten Berlin",
            Function = "Schatzmeister",
            MotionDate = new DateTime(2026, 4, 15),
            DonatedAmount = 25m,
            PayoutAmount = 75m,
            PayoutMethod = ExpensePayoutMethodDTO.BankTransfer,
            RequestsDonationReceipt = true,
            Iban = "DE89370400440532013000",
            SignatureLocation = "Berlin",
            FormDate = new DateTime(2026, 6, 6),
            LineItems = new List<ExpenseReimbursementLineItemDTO> {
                new() { Description = "Druckkosten Plakate", Value = 40m },
                new() { Description = "Versandkosten Materialversand", Value = 35m },
                new() { Description = "Webhosting Mai 2026", Value = 25m }
            }
        };
        return new Dictionary<string, object> {
            ["form"] = sample,
            ["globals"] = new Dictionary<string, object?> {
                ["BaseUrl"] = "https://quartermaster.example.de",
                ["AppName"] = "Quartermaster",
                ["Now"] = new DateTime(2026, 6, 6)
            }
        };
    }
}
