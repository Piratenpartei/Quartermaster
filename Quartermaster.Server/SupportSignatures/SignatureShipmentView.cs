using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures;

public static class SignatureShipmentView {
    /// <summary>A shipment with its lines and returns; overdue once <paramref name="overdueDays"/> have passed since sending while forms are still open.</summary>
    public static SignatureShipmentDTO Build(SignatureShipment shipment, string officeName, List<SignatureShipmentLine> lines,
        List<SignatureReturn> returns, List<SignatureReturnLine> allReturnLines, Dictionary<Guid, string> municipalityNames, int overdueDays, DateOnly today) {
        var returnIds = returns.Select(r => r.Id).ToHashSet();
        var returnLines = allReturnLines
            .Where(l => returnIds.Contains(l.ReturnId))
            .ToList();

        var lineDtos = lines
            .Select(l => Line(l, returnLines.Where(r => r.AdministrativeDivisionId == l.AdministrativeDivisionId).ToList(), municipalityNames))
            .OrderBy(l => l.MunicipalityName)
            .ToList();
        var open = lineDtos.Sum(l => l.Open);
        DateOnly? sentOn = shipment.SentOn == null ? null : DateOnly.FromDateTime(shipment.SentOn.Value);

        return new SignatureShipmentDTO {
            Id = shipment.Id,
            OfficeId = shipment.OfficeId,
            OfficeName = officeName,
            SentOn = sentOn,
            Lines = lineDtos,
            Returns = returns
                .Select(r => new SignatureReturnDTO {
                    Id = r.Id,
                    ReturnedOn = DateOnly.FromDateTime(r.ReturnedOn),
                    Certified = returnLines.Where(l => l.ReturnId == r.Id).Sum(l => l.Certified),
                    Rejected = returnLines.Where(l => l.ReturnId == r.Id).Sum(l => l.Rejected),
                    Lines = returnLines
                        .Where(l => l.ReturnId == r.Id)
                        .Select(l => new ReturnLineInput {
                            MunicipalityId = l.AdministrativeDivisionId,
                            Certified = l.Certified,
                            Rejected = l.Rejected,
                            RejectionReason = l.RejectionReason
                        })
                        .ToList()
                })
                .ToList(),
            Open = open,
            IsOverdue = sentOn != null && open > 0 && sentOn.Value.AddDays(overdueDays) < today
        };
    }

    private static ShipmentLineDTO Line(SignatureShipmentLine line, List<SignatureReturnLine> returnLines, Dictionary<Guid, string> municipalityNames) {
        var certified = returnLines.Sum(l => l.Certified);
        var rejected = returnLines.Sum(l => l.Rejected);
        return new ShipmentLineDTO {
            MunicipalityId = line.AdministrativeDivisionId,
            MunicipalityName = municipalityNames.GetValueOrDefault(line.AdministrativeDivisionId, ""),
            Sent = line.Count,
            Certified = certified,
            Rejected = rejected,
            Open = line.Count - certified - rejected
        };
    }
}
