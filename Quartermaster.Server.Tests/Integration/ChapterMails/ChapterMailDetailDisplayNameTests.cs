using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.ChapterMails;
using Quartermaster.Api.Users;
using Quartermaster.Data.ChapterMails;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.ChapterMails;

/// <summary>Who wrote and sent a chapter mail is informal, so it shows the display name each of them chose.</summary>
public class ChapterMailDetailDisplayNameTests : IntegrationTestBase {
    [Test]
    public async Task Author_appears_under_the_display_name_they_chose() {
        var chapter = Builder.SeedChapter("Kreis");
        var author = Builder.SeedUser(firstName: "Erika", lastName: "Mustermann", username: "erika.mustermann");
        Db.Users.Where(u => u.Id == author.Id).Set(u => u.DisplayNameMode, DisplayNameMode.Username).Update();
        var mail = new ChapterMail {
            ChapterId = chapter.Id,
            Title = "Einladung",
            TargetType = ChapterMailTargetType.Chapter,
            TargetId = chapter.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = author.Id
        };
        Db.Insert(mail);
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ManageChapterMail } });
        using var client = AuthenticatedClient(token);

        var detail = await client.GetFromJsonAsync<ChapterMailDTO>($"/api/chapter-mail/{mail.Id}");

        await Assert.That(detail!.CreatedByDisplayName).IsEqualTo("erika.mustermann");
    }
}
