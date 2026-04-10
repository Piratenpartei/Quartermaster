using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Server.MembershipApplications;

namespace Quartermaster.Server.Tests.MembershipApplications;

public class MembershipApplicationDTOValidatorTests {
    private readonly MembershipApplicationDTOValidator _validator = new();

    private static MembershipApplicationDTO ValidApplication() => new() {
        FirstName = "Max",
        LastName = "Mustermann",
        DateOfBirth = new DateTime(1990, 1, 1),
        Citizenship = "deutsch",
        EMail = "max@example.com",
        PhoneNumber = "0123456789",
        AddressStreet = "Musterstraße",
        AddressHouseNbr = "42",
        AddressPostCode = "12345",
        AddressCity = "Berlin",
        AddressAdministrativeDivisionId = Guid.NewGuid(),
        ChapterId = Guid.NewGuid(),
        ConformityDeclarationAccepted = true,
        HasPriorDeclinedApplication = false,
        IsMemberOfAnotherParty = false,
        ApplicationText = "",
        EntryDate = new DateTime(2026, 1, 1)
    };

    [Test]
    public void ValidApplication_ShouldHaveNoErrors() {
        var application = ValidApplication();

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyFirstName_ShouldHaveError() {
        var application = ValidApplication();
        application.FirstName = "";

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.FirstName)
            .WithErrorMessage("Vorname darf nicht leer sein.");
    }

    [Test]
    public void FirstNameExceedsMaxLength_ShouldHaveError() {
        var application = ValidApplication();
        application.FirstName = new string('A', 257);

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.FirstName)
            .WithErrorMessage("Vorname darf maximal 256 Zeichen lang sein.");
    }

    [Test]
    public void FirstNameAtMaxLength_ShouldHaveNoError() {
        var application = ValidApplication();
        application.FirstName = new string('A', 256);

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveValidationErrorFor(x => x.FirstName);
    }

    [Test]
    public void EmptyLastName_ShouldHaveError() {
        var application = ValidApplication();
        application.LastName = "";

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.LastName)
            .WithErrorMessage("Nachname darf nicht leer sein.");
    }

    [Test]
    public void LastNameExceedsMaxLength_ShouldHaveError() {
        var application = ValidApplication();
        application.LastName = new string('A', 257);

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.LastName)
            .WithErrorMessage("Nachname darf maximal 256 Zeichen lang sein.");
    }

    [Test]
    public void LastNameAtMaxLength_ShouldHaveNoError() {
        var application = ValidApplication();
        application.LastName = new string('A', 256);

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveValidationErrorFor(x => x.LastName);
    }

    [Test]
    public void EmptyEMail_ShouldHaveError() {
        var application = ValidApplication();
        application.EMail = "";

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.EMail)
            .WithErrorMessage("E-Mail-Adresse darf nicht leer sein.");
    }

    [Test]
    public void EMailWithoutAtSign_ShouldHaveError() {
        var application = ValidApplication();
        application.EMail = "invalid-email";

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.EMail)
            .WithErrorMessage("E-Mail-Adresse muss ein @ enthalten.");
    }

    [Test]
    public void EMailExceedsMaxLength_ShouldHaveError() {
        var application = ValidApplication();
        application.EMail = new string('a', 250) + "@ab.com";

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.EMail)
            .WithErrorMessage("E-Mail-Adresse darf maximal 256 Zeichen lang sein.");
    }

    [Test]
    public void EMailAtMaxLength_ShouldHaveNoError() {
        var application = ValidApplication();
        application.EMail = new string('a', 249) + "@ab.com";

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveValidationErrorFor(x => x.EMail);
    }

    [Test]
    public void ValidEMail_ShouldHaveNoError() {
        var application = ValidApplication();
        application.EMail = "test@example.com";

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveValidationErrorFor(x => x.EMail);
    }

    [Test]
    public void EmptyCitizenship_ShouldHaveError() {
        var application = ValidApplication();
        application.Citizenship = "";

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.Citizenship)
            .WithErrorMessage("Staatsangehörigkeit darf nicht leer sein.");
    }

    [Test]
    public void CitizenshipExceedsMaxLength_ShouldHaveError() {
        var application = ValidApplication();
        application.Citizenship = new string('A', 257);

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.Citizenship)
            .WithErrorMessage("Staatsangehörigkeit darf maximal 256 Zeichen lang sein.");
    }

    [Test]
    public void CitizenshipAtMaxLength_ShouldHaveNoError() {
        var application = ValidApplication();
        application.Citizenship = new string('A', 256);

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveValidationErrorFor(x => x.Citizenship);
    }

    [Test]
    public void EmptyPhoneNumber_ShouldHaveNoError() {
        var application = ValidApplication();
        application.PhoneNumber = "";

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Test]
    public void PhoneNumberExceedsMaxLength_ShouldHaveError() {
        var application = ValidApplication();
        application.PhoneNumber = new string('1', 65);

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber)
            .WithErrorMessage("Telefonnummer darf maximal 64 Zeichen lang sein.");
    }

    [Test]
    public void PhoneNumberAtMaxLength_ShouldHaveNoError() {
        var application = ValidApplication();
        application.PhoneNumber = new string('1', 64);

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Test]
    public void EmptyAddressStreet_ShouldHaveError() {
        var application = ValidApplication();
        application.AddressStreet = "";

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.AddressStreet)
            .WithErrorMessage("Straße darf nicht leer sein.");
    }

    [Test]
    public void AddressStreetExceedsMaxLength_ShouldHaveError() {
        var application = ValidApplication();
        application.AddressStreet = new string('A', 257);

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.AddressStreet)
            .WithErrorMessage("Straße darf maximal 256 Zeichen lang sein.");
    }

    [Test]
    public void AddressStreetAtMaxLength_ShouldHaveNoError() {
        var application = ValidApplication();
        application.AddressStreet = new string('A', 256);

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveValidationErrorFor(x => x.AddressStreet);
    }

    [Test]
    public void EmptyAddressHouseNbr_ShouldHaveError() {
        var application = ValidApplication();
        application.AddressHouseNbr = "";

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.AddressHouseNbr)
            .WithErrorMessage("Hausnummer darf nicht leer sein.");
    }

    [Test]
    public void AddressHouseNbrExceedsMaxLength_ShouldHaveError() {
        var application = ValidApplication();
        application.AddressHouseNbr = new string('1', 33);

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.AddressHouseNbr)
            .WithErrorMessage("Hausnummer darf maximal 32 Zeichen lang sein.");
    }

    [Test]
    public void AddressHouseNbrAtMaxLength_ShouldHaveNoError() {
        var application = ValidApplication();
        application.AddressHouseNbr = new string('1', 32);

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveValidationErrorFor(x => x.AddressHouseNbr);
    }

    [Test]
    public void EmptyAddressPostCode_ShouldHaveError() {
        var application = ValidApplication();
        application.AddressPostCode = "";

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.AddressPostCode)
            .WithErrorMessage("Postleitzahl darf nicht leer sein.");
    }

    [Test]
    public void AddressPostCodeExceedsMaxLength_ShouldHaveError() {
        var application = ValidApplication();
        application.AddressPostCode = new string('1', 17);

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.AddressPostCode)
            .WithErrorMessage("Postleitzahl darf maximal 16 Zeichen lang sein.");
    }

    [Test]
    public void AddressPostCodeAtMaxLength_ShouldHaveNoError() {
        var application = ValidApplication();
        application.AddressPostCode = new string('1', 16);

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveValidationErrorFor(x => x.AddressPostCode);
    }

    [Test]
    public void EmptyAddressCity_ShouldHaveError() {
        var application = ValidApplication();
        application.AddressCity = "";

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.AddressCity)
            .WithErrorMessage("Stadt darf nicht leer sein.");
    }

    [Test]
    public void AddressCityExceedsMaxLength_ShouldHaveError() {
        var application = ValidApplication();
        application.AddressCity = new string('A', 257);

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.AddressCity)
            .WithErrorMessage("Stadt darf maximal 256 Zeichen lang sein.");
    }

    [Test]
    public void AddressCityAtMaxLength_ShouldHaveNoError() {
        var application = ValidApplication();
        application.AddressCity = new string('A', 256);

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveValidationErrorFor(x => x.AddressCity);
    }

    [Test]
    public void EmptyApplicationText_ShouldHaveNoError() {
        var application = ValidApplication();
        application.ApplicationText = "";

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveValidationErrorFor(x => x.ApplicationText);
    }

    [Test]
    public void ApplicationTextExceedsMaxLength_ShouldHaveError() {
        var application = ValidApplication();
        application.ApplicationText = new string('A', 2049);

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.ApplicationText)
            .WithErrorMessage("Antragstext darf maximal 2048 Zeichen lang sein.");
    }

    [Test]
    public void ApplicationTextAtMaxLength_ShouldHaveNoError() {
        var application = ValidApplication();
        application.ApplicationText = new string('A', 2048);

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveValidationErrorFor(x => x.ApplicationText);
    }

    [Test]
    public void ConformityDeclarationNotAccepted_ShouldHaveError() {
        var application = ValidApplication();
        application.ConformityDeclarationAccepted = false;

        var result = _validator.TestValidate(application);

        result.ShouldHaveValidationErrorFor(x => x.ConformityDeclarationAccepted)
            .WithErrorMessage("Die Grundsatzerklärung muss akzeptiert werden.");
    }

    [Test]
    public void ConformityDeclarationAccepted_ShouldHaveNoError() {
        var application = ValidApplication();
        application.ConformityDeclarationAccepted = true;

        var result = _validator.TestValidate(application);

        result.ShouldNotHaveValidationErrorFor(x => x.ConformityDeclarationAccepted);
    }
}
