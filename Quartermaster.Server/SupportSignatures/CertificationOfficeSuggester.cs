using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures;

/// <summary>
/// Suggests the office for a municipality: its own office, the office a fellow member of its Gemeindeverband already has,
/// a new office for the Gemeindeverband with all members that have none yet, or else the municipality's own administration.
/// </summary>
public class CertificationOfficeSuggester {
    private static readonly Dictionary<int, string> _kindPrefixes = new() {
        [51] = "Amt",
        [52] = "Samtgemeinde",
        [53] = "Verbandsgemeinde",
        [54] = "Verwaltungsgemeinschaft",
        [56] = "Verwaltungsverband"
    };

    private readonly AdministrativeDivisionRepository _divisions;
    private readonly CertificationOfficeRepository _offices;

    public CertificationOfficeSuggester(AdministrativeDivisionRepository divisions, CertificationOfficeRepository offices) {
        _divisions = divisions;
        _offices = offices;
    }

    public OfficeSuggestionDTO? Suggest(Guid municipalityId) {
        var municipality = _divisions.Get(municipalityId);
        if (municipality == null || municipality.Depth != AdministrativeDivisionRepository.MunicipalityDepth)
            return null;

        var ownOffice = _offices.GetOfficeIdByMunicipality(municipalityId);
        if (ownOffice != null)
            return Suggestion(municipality, ownOffice, "", [municipality]);

        var association = AssociationCertifyingFor(municipality);
        if (association == null)
            return Suggestion(municipality, null, municipality.Name, [municipality]);

        var members = _divisions.GetMunicipalitiesOfAssociation(association.Key);
        var officeByMember = _offices.GetOfficeIdsByMunicipality(members.Select(m => m.Id));
        var withoutOffice = members
            .Where(m => !officeByMember.ContainsKey(m.Id))
            .ToList();
        var siblingOffice = officeByMember.Values.Distinct().ToList();

        return siblingOffice.Count == 1
            ? Suggestion(municipality, siblingOffice[0], "", withoutOffice)
            : Suggestion(municipality, null, OfficeName(association), withoutOffice);
    }

    private MunicipalityAssociation? AssociationCertifyingFor(AdministrativeDivision municipality) {
        if (string.IsNullOrEmpty(municipality.MunicipalityAssociationKey))
            return null;

        var association = _divisions.GetAssociation(municipality.MunicipalityAssociationKey);
        if (association == null || !Gv100AdParser.CertifiesForMembers(association.Kind))
            return null;

        return association;
    }

    private static string OfficeName(MunicipalityAssociation association) {
        if (!_kindPrefixes.TryGetValue(association.Kind, out var prefix))
            return association.Name;

        return association.Name.StartsWith(prefix, StringComparison.Ordinal) ? association.Name : $"{prefix} {association.Name}";
    }

    private static OfficeSuggestionDTO Suggestion(AdministrativeDivision municipality, Guid? officeId, string name, List<AdministrativeDivision> municipalities) {
        return new OfficeSuggestionDTO {
            MunicipalityId = municipality.Id,
            ExistingOfficeId = officeId,
            SuggestedName = name,
            Municipalities = municipalities
                .Select(m => new OfficeMunicipalityDTO { Id = m.Id, Name = m.Name })
                .ToList()
        };
    }
}
