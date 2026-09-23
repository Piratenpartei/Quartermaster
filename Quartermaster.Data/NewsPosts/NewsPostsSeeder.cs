using System.Collections.Generic;

namespace Quartermaster.Data.NewsPosts;

/// <summary>Seeds the V1 federal feeds and programme sources only on first install (both tables still empty);
/// once anything exists, it never adds, restores or duplicates a row, so admin edits and deletions survive
/// restarts.</summary>
public static class NewsPostsSeeder {
    private static readonly List<NewsFeed> DefaultFeeds = [
        new NewsFeed { Name = "netzpolitik.org", Url = "https://netzpolitik.org/feed/", Kind = NewsFeedKind.Rss, IsEnabled = true },
        new NewsFeed { Name = "Bundestag hib", Url = "https://www.bundestag.de/static/appdata/includes/rss/hib.rss", Kind = NewsFeedKind.Rss, IsEnabled = true },
        new NewsFeed { Name = "tagesschau", Url = "https://www.tagesschau.de/api2u/news/", Kind = NewsFeedKind.TagesschauApi, IsEnabled = true }
    ];

    private static readonly List<string> DefaultSources = [
        "Parteiprogramm",
        "Bundestagswahl 2025/Wahlprogramm",
        "Positionspapiere/"
    ];

    public static void Seed(NewsFeedRepository feedRepo, ProgrammeRepository programmeRepo) {
        if (feedRepo.GetAll().Count > 0 || programmeRepo.GetSources().Count > 0)
            return;

        foreach (var feed in DefaultFeeds)
            feedRepo.Create(new NewsFeed { Name = feed.Name, Url = feed.Url, Kind = feed.Kind, IsEnabled = feed.IsEnabled });
        foreach (var title in DefaultSources)
            programmeRepo.CreateSource(new ProgrammeSource { WikiPageTitle = title });
    }
}
