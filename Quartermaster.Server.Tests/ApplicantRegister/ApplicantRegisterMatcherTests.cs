using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Server.ApplicantRegister;

namespace Quartermaster.Server.Tests.ApplicantRegister;

public class ApplicantRegisterMatcherTests {
    private static RegisterPerson Person(
        string firstName = "",
        string lastName = "",
        DateOnly? dateOfBirth = null,
        string? street = null,
        string? houseNumber = null,
        string? postCode = null,
        string? city = null,
        string? email = null) {
        return new RegisterPerson(firstName, lastName, dateOfBirth, street, houseNumber, postCode, city, email);
    }

    [Test]
    [Arguments("Jörg", "Müller", "Joerg", "Mueller")]
    [Arguments("Jörg", "Müller", "Jorg", "Muller")]
    public async Task Umlaut_and_ascii_spelling_match(string firstA, string lastA, string firstB, string lastB) {
        var fields = ApplicantRegisterMatcher.Compare(Person(firstA, lastA), Person(firstB, lastB));

        await Assert.That(fields).IsEqualTo(RegisterMatchFields.FirstName | RegisterMatchFields.LastName);
    }

    [Test]
    [Arguments("Schmidt", "Schmitt", true)]
    [Arguments("Alexandra", "Alexandar", true)]
    [Arguments("Anna", "Ania", true)]
    [Arguments("Anna", "Arno", false)]
    public async Task Typo_matches_within_distance(string a, string b, bool expected) {
        await Assert.That(ApplicantRegisterMatcher.NamesMatch(a, b)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("Anna Maria", "Anna")]
    [Arguments("Maria Anna", "Anna Maria")]
    public async Task Dropped_and_swapped_middle_names_match(string a, string b) {
        await Assert.That(ApplicantRegisterMatcher.NamesMatch(a, b)).IsTrue();
    }

    [Test]
    public async Task Double_last_name_matches_part() {
        await Assert.That(ApplicantRegisterMatcher.NamesMatch("Müller-Lüdenscheidt", "Müller")).IsTrue();
    }

    [Test]
    [Arguments("Hauptstraße", "12a", "Hauptstr.", "12 A", true)]
    [Arguments("Haupt Straße", "1", "Hauptstrasse", "1", true)]
    [Arguments("Hauptstraße", "12", "Hauptstraße", "14", false)]
    public async Task Street_folds_strasse(string streetA, string numberA, string streetB, string numberB, bool expected) {
        var fields = ApplicantRegisterMatcher.Compare(Person(street: streetA, houseNumber: numberA), Person(street: streetB, houseNumber: numberB));

        await Assert.That(fields.HasFlag(RegisterMatchFields.Street)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("37073", "Göttingen", "37073", "Goettingen", true)]
    [Arguments("37073", "Göttingen", "37073", "Kassel", false)]
    [Arguments("37073", "Göttingen", "34117", "Göttingen", false)]
    public async Task Post_code_and_city_both_needed(string postCodeA, string cityA, string postCodeB, string cityB, bool expected) {
        var fields = ApplicantRegisterMatcher.Compare(Person(postCode: postCodeA, city: cityA), Person(postCode: postCodeB, city: cityB));

        await Assert.That(fields.HasFlag(RegisterMatchFields.PostCodeCity)).IsEqualTo(expected);
    }

    [Test]
    public async Task Email_ignores_case_and_whitespace() {
        var fields = ApplicantRegisterMatcher.Compare(Person(email: "  Anna@Example.INVALID "), Person(email: "anna@example.invalid"));

        await Assert.That(fields).IsEqualTo(RegisterMatchFields.Email);
    }

    [Test]
    public async Task Empty_fields_never_match() {
        var fields = ApplicantRegisterMatcher.Compare(
            Person("Anna", "Schmidt", email: "  ", street: "", houseNumber: "", postCode: "", city: ""),
            Person("Anna", "Schmidt", email: "", street: "", houseNumber: "", postCode: "", city: ""));

        await Assert.That(fields).IsEqualTo(RegisterMatchFields.FirstName | RegisterMatchFields.LastName);
    }

    [Test]
    public async Task Three_fields_make_a_hit() {
        var entryId = Guid.NewGuid();
        var entry = new ApplicantRegisterEntry {
            Id = entryId,
            FirstName = "Anna",
            LastName = "Schmidt",
            DateOfBirth = new DateTime(1990, 5, 17),
            Street = "Hauptstraße",
            HouseNumber = "12",
            Email = "anna@example.invalid"
        };
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        IReadOnlyList<ApplicantRegisterEntry> entries = [entry];

        var withStreet = ApplicantRegisterMatcher.Match(Person("Anna", "Schmidt", street: "Hauptstr.", houseNumber: "12"), entries, now);
        var namesOnly = ApplicantRegisterMatcher.Match(Person("Anna", "Schmidt"), entries, now);
        var withEmail = ApplicantRegisterMatcher.Match(Person("Jens", "Schmidt", new DateOnly(1990, 5, 17), email: "ANNA@example.invalid"), entries, now);

        await Assert.That(withStreet.Count).IsEqualTo(1);
        await Assert.That(withStreet[0].EntryId).IsEqualTo(entryId);
        await Assert.That(withStreet[0].ApplicationId).IsEqualTo(Guid.Empty);
        await Assert.That(withStreet[0].CreatedAt).IsEqualTo(now);
        await Assert.That(withStreet[0].MatchedFields).IsEqualTo(RegisterMatchFields.FirstName | RegisterMatchFields.LastName | RegisterMatchFields.Street);
        await Assert.That(namesOnly.Count).IsEqualTo(0);
        await Assert.That(withEmail.Count).IsEqualTo(1);
        await Assert.That(withEmail[0].MatchedFields).IsEqualTo(RegisterMatchFields.LastName | RegisterMatchFields.DateOfBirth | RegisterMatchFields.Email);
    }

    [Test]
    public async Task House_number_without_street_does_not_match_street() {
        var fields = ApplicantRegisterMatcher.Compare(Person(houseNumber: "12"), Person(houseNumber: "12"));

        await Assert.That(fields).IsEqualTo(RegisterMatchFields.None);
    }

    [Test]
    public async Task Street_without_house_number_does_not_match_street() {
        var fields = ApplicantRegisterMatcher.Compare(Person(street: "Hauptstraße"), Person(street: "Hauptstraße"));

        await Assert.That(fields).IsEqualTo(RegisterMatchFields.None);
    }

    [Test]
    public async Task Entry_without_date_of_birth_and_email_matches_on_three_other_fields() {
        var entry = new ApplicantRegisterEntry {
            Id = Guid.NewGuid(),
            FirstName = "Anna",
            LastName = "Schmidt",
            Street = "Hauptstraße",
            HouseNumber = "12"
        };
        var candidate = Person("Anna", "Schmidt", new DateOnly(1990, 5, 17), "Hauptstr.", "12", email: "anna@example.invalid");

        var hits = ApplicantRegisterMatcher.Match(candidate, [entry], DateTime.UtcNow);

        await Assert.That(hits.Count).IsEqualTo(1);
        await Assert.That(hits[0].MatchedFields).IsEqualTo(RegisterMatchFields.FirstName | RegisterMatchFields.LastName | RegisterMatchFields.Street);
        await Assert.That(ApplicantRegisterMatcher.CountFields(hits[0].MatchedFields)).IsEqualTo(3);
    }

    [Test]
    public async Task Count_fields_ignores_none() {
        await Assert.That(ApplicantRegisterMatcher.CountFields(RegisterMatchFields.None)).IsEqualTo(0);
        await Assert.That(ApplicantRegisterMatcher.CountFields(RegisterMatchFields.Street | RegisterMatchFields.Email)).IsEqualTo(2);
    }
}
