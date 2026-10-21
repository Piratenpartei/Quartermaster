using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.SupportSignatures;

public class SignatureShipmentRepository {
    private readonly DbContext _context;

    public SignatureShipmentRepository(DbContext context) {
        _context = context;
    }

    public SignatureShipment? Get(Guid id) {
        return _context.SignatureShipments.FirstOrDefault(s => s.Id == id);
    }

    public List<SignatureShipment> List(Guid campaignId) {
        return _context.SignatureShipments
            .Where(s => s.CampaignId == campaignId)
            .OrderBy(s => s.SentOn == null ? 0 : 1)
            .ThenBy(s => s.SentOn)
            .ThenBy(s => s.CreatedAt)
            .ToList();
    }

    public List<SignatureShipmentLine> GetLines(IEnumerable<Guid> shipmentIds) {
        var ids = shipmentIds.Distinct().ToList();
        return _context.SignatureShipmentLines
            .Where(l => ids.Contains(l.ShipmentId))
            .ToList();
    }

    public SignatureReturn? GetReturn(Guid id) {
        return _context.SignatureReturns.FirstOrDefault(r => r.Id == id);
    }

    public List<SignatureReturn> GetReturns(IEnumerable<Guid> shipmentIds) {
        var ids = shipmentIds.Distinct().ToList();
        return _context.SignatureReturns
            .Where(r => ids.Contains(r.ShipmentId))
            .OrderBy(r => r.ReturnedOn)
            .ThenBy(r => r.CreatedAt)
            .ToList();
    }

    public List<SignatureReturnLine> GetReturnLines(IEnumerable<Guid> returnIds) {
        var ids = returnIds.Distinct().ToList();
        return _context.SignatureReturnLines
            .Where(l => ids.Contains(l.ReturnId))
            .ToList();
    }

    public void Create(SignatureShipment shipment, IEnumerable<SignatureShipmentLine> lines) {
        _context.Insert(shipment);
        foreach (var line in lines) {
            line.ShipmentId = shipment.Id;
            _context.Insert(line);
        }
    }

    public void SetSentOn(Guid id, DateTime sentOn) {
        _context.SignatureShipments
            .Where(s => s.Id == id)
            .Set(s => s.SentOn, sentOn)
            .Update();
    }

    public void Delete(Guid id) {
        _context.SignatureShipmentLines
            .Where(l => l.ShipmentId == id)
            .Delete();
        _context.SignatureShipments
            .Where(s => s.Id == id)
            .Delete();
    }

    public void CreateReturn(SignatureReturn signatureReturn, IEnumerable<SignatureReturnLine> lines) {
        _context.Insert(signatureReturn);
        foreach (var line in lines) {
            line.ReturnId = signatureReturn.Id;
            _context.Insert(line);
        }
    }

    public void ReplaceReturnLines(Guid returnId, DateTime returnedOn, IEnumerable<SignatureReturnLine> lines) {
        _context.SignatureReturns
            .Where(r => r.Id == returnId)
            .Set(r => r.ReturnedOn, returnedOn)
            .Update();
        _context.SignatureReturnLines
            .Where(l => l.ReturnId == returnId)
            .Delete();
        foreach (var line in lines) {
            line.ReturnId = returnId;
            _context.Insert(line);
        }
    }

    public void DeleteReturn(Guid id) {
        _context.SignatureReturnLines
            .Where(l => l.ReturnId == id)
            .Delete();
        _context.SignatureReturns
            .Where(r => r.Id == id)
            .Delete();
    }
}
