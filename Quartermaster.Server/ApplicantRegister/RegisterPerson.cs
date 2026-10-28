using System;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.ApplicantRegister;

namespace Quartermaster.Server.ApplicantRegister;

public record RegisterPerson(
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    string? Street,
    string? HouseNumber,
    string? PostCode,
    string? City,
    string? Email
) {
    public static RegisterPerson FromApplication(MembershipApplicationDTO dto) {
        return new RegisterPerson(
            dto.FirstName,
            dto.LastName,
            dto.DateOfBirth,
            dto.AddressStreet,
            dto.AddressHouseNbr,
            dto.AddressPostCode,
            dto.AddressCity,
            dto.Email);
    }

    public static RegisterPerson FromEntry(ApplicantRegisterEntry entry) {
        return new RegisterPerson(
            entry.FirstName,
            entry.LastName,
            entry.DateOfBirth.HasValue ? DateOnly.FromDateTime(entry.DateOfBirth.Value) : null,
            entry.Street,
            entry.HouseNumber,
            entry.PostCode,
            entry.City,
            entry.Email);
    }
}
