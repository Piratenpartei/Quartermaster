using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures;

/// <summary>
/// Suggests the hand-entered office for a municipality: its own one, or the one a fellow municipality of the same administration
/// already has, or else a new one. A new one is prefilled from the official address directory with all municipalities of that
/// administration; without a directory entry it is named after the Gemeindeverband from GV100AD, or the municipality itself.
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

        var ownOffice = _offices.GetCustomOfficeIdByMunicipality(municipalityId);
        if (ownOffice != null)
            return Suggestion(municipality, ownOffice, "", [municipality]);

        var officialId = _offices.GetOfficialOfficeIdByMunicipality(municipalityId);
        var official = officialId == null ? null : _offices.Get(officialId.Value);
        if (official != null)
            return FromOfficial(municipality, official);

        var association = AssociationCertifyingFor(municipality);
        if (association == null)
            return Suggestion(municipality, null, municipality.Name, [municipality]);

        return FromMembers(municipality, _divisions.GetMunicipalitiesOfAssociation(association.Key), OfficeName(association));
    }

    private OfficeSuggestionDTO FromOfficial(AdministrativeDivision municipality, CertificationOffice official) {
        var members = _divisions.GetMany(_offices.GetOfficialMunicipalityIds(official.Id))
            .OrderBy(d => d.Name)
            .ToList();
        var suggestion = FromMembers(municipality, members, official.Name);
        if (suggestion.ExistingOfficeId != null)
            return suggestion;

        suggestion.Street = official.Street;
        suggestion.PostCode = official.PostCode;
        suggestion.City = official.City;
        suggestion.Email = official.Email;
        suggestion.FromOfficialDirectory = true;
        return suggestion;
    }

    /// <summary>The hand-entered office the members already share, else a new one with all members that have none yet.</summary>
    private OfficeSuggestionDTO FromMembers(AdministrativeDivision municipality, List<AdministrativeDivision> members, string name) {
        var officeByMember = _offices.GetCustomOfficeIdsByMunicipality(members.Select(m => m.Id));
        var withoutOffice = members
            .Where(m => !officeByMember.ContainsKey(m.Id))
            .ToList();
        var siblingOffice = officeByMember.Values.Distinct().ToList();

        return siblingOffice.Count == 1
            ? Suggestion(municipality, siblingOffice[0], "", withoutOffice)
            : Suggestion(municipality, null, name, withoutOffice);
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
