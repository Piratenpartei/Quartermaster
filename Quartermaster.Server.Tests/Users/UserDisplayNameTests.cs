using System.Threading.Tasks;
using Quartermaster.Api.Users;
using Quartermaster.Data.Users;

namespace Quartermaster.Server.Tests.Users;

public class UserDisplayNameTests {
    private static User Erika(DisplayNameMode mode, string? username = "erika.mustermann") {
        return new User {
            FirstName = "Erika",
            LastName = "Mustermann",
            Email = "erika@example.org",
            Username = username,
            DisplayNameMode = mode
        };
    }

    [Test]
    public async Task First_and_last_name_by_default() {
        await Assert.That(Erika(DisplayNameMode.RealName).DisplayName()).IsEqualTo("Erika Mustermann");
    }

    [Test]
    public async Task The_username_when_chosen() {
        await Assert.That(Erika(DisplayNameMode.Username).DisplayName()).IsEqualTo("erika.mustermann");
    }

    /// <summary>Before their first login after V9.1 SSO accounts still hold the e-mail as username; it is never shown as a name.</summary>
    [Test]
    public async Task The_real_name_while_the_username_is_still_the_email() {
        await Assert.That(Erika(DisplayNameMode.Username, "erika@example.org").DisplayName()).IsEqualTo("Erika Mustermann");
        await Assert.That(Erika(DisplayNameMode.Username, null).DisplayName()).IsEqualTo("Erika Mustermann");
    }
}
