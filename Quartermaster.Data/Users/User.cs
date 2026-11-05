using System;
using LinqToDB.Mapping;
using Quartermaster.Api.Users;

namespace Quartermaster.Data.Users;

[Table(TableName, IsColumnAttributeRequired = false)]
public class User {
    public const string TableName = "Users";
    public const int KeycloakUserIdMaxLength = 64;
    public const int UsernameMaxLength = 255;

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? Username { get; set; }
    public string Email { get; set; } = "";
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
    public DateTime? DeletedAt { get; set; }

    /// <summary>Telegram chat id (numeric string) once the user has consumed a link token via the bot's <c>/start</c> deeplink. Null = not linked.</summary>
    public string? TelegramChatId { get; set; }

    /// <summary>The <c>sub</c> claim of the last OIDC login, which the forum stores as the account's external id.</summary>
    public string? KeycloakUserId { get; set; }

    public DisplayNameMode DisplayNameMode { get; set; }
}