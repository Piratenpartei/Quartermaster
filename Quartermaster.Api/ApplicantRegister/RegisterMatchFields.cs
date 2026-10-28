using System;

namespace Quartermaster.Api.ApplicantRegister;

[Flags]
public enum RegisterMatchFields {
    None = 0,
    FirstName = 1,
    LastName = 2,
    DateOfBirth = 4,
    Street = 8,
    PostCodeCity = 16,
    Email = 32
}
