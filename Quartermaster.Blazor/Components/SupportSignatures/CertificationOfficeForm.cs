using System.Linq;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Blazor.Components.SupportSignatures;

public static class CertificationOfficeForm {
    public static CertificationOfficeSaveRequest ToRequest(CertificationOfficeDTO office) {
        return new CertificationOfficeSaveRequest {
            Id = office.Id,
            Name = office.Name,
            AddressAddition = office.AddressAddition,
            Street = office.Street,
            PostCode = office.PostCode,
            City = office.City,
            Email = office.Email,
            Phone = office.Phone,
            Notes = office.Notes,
            MunicipalityIds = office.Municipalities.Select(m => m.Id).ToList()
        };
    }
}
