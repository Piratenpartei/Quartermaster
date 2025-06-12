using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Users;

[Table("Users", IsColumnAttributeRequired = false)]
public class User {
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? Username { get; set; }
    public string EMail { get; set; } = "";
    public string? PasswordHash { get; set; }

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime DateOfBirth { get; set; }
    public Guid CitizenshipAdministrativeDivisionId { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal MembershipFee { get; set; }
    public DateTime MemberSince { get; set; }
    public int MemberNumber { get; set; }

    public string AddressStreet { get; set; } = "";
    public string AddressHouseNbr { get; set; } = "";
    public Guid AddressAdministrativeDivisionId { get; set; }

    public Guid? ChapterId { get; set; }
}