using System;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.SupportSignatures;

/// <summary>
/// Moves all support signature counts from a division that an import merges away to its replacement.
/// An office link the replacement already has wins, and shipment lines of the same shipment are added up, because both are unique per division.
/// </summary>
public static class SupportSignatureDivisionRemap {
    public static int Apply(DbContext context, Guid fromId, Guid toId) {
        var moved = context.SignatureIntakes
            .Where(i => i.AdministrativeDivisionId == fromId)
            .Set(i => i.AdministrativeDivisionId, toId)
            .Update();

        moved += MoveOfficeLink(context, fromId, toId);
        moved += MoveShipmentLines(context, fromId, toId);

        moved += context.SignatureReturnLines
            .Where(l => l.AdministrativeDivisionId == fromId)
            .Set(l => l.AdministrativeDivisionId, toId)
            .Update();
        return moved;
    }

    private static int MoveOfficeLink(DbContext context, Guid fromId, Guid toId) {
        var links = context.CertificationOfficeMunicipalities.Where(m => m.AdministrativeDivisionId == fromId);
        if (context.CertificationOfficeMunicipalities.Any(m => m.AdministrativeDivisionId == toId))
            return links.Delete();

        return links
            .Set(m => m.AdministrativeDivisionId, toId)
            .Update();
    }

    private static int MoveShipmentLines(DbContext context, Guid fromId, Guid toId) {
        var lines = context.SignatureShipmentLines
            .Where(l => l.AdministrativeDivisionId == fromId)
            .ToList();
        foreach (var line in lines) {
            var target = context.SignatureShipmentLines.FirstOrDefault(l => l.ShipmentId == line.ShipmentId && l.AdministrativeDivisionId == toId);
            if (target == null) {
                context.SignatureShipmentLines
                    .Where(l => l.Id == line.Id)
                    .Set(l => l.AdministrativeDivisionId, toId)
                    .Update();
                continue;
            }

            context.SignatureShipmentLines
                .Where(l => l.Id == target.Id)
                .Set(l => l.Count, target.Count + line.Count)
                .Update();
            context.SignatureShipmentLines
                .Where(l => l.Id == line.Id)
                .Delete();
        }
        return lines.Count;
    }
}
