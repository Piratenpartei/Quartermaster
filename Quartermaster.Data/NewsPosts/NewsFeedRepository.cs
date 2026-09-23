using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.NewsPosts;

public class NewsFeedRepository {
    private readonly DbContext _context;

    public NewsFeedRepository(DbContext context) {
        _context = context;
    }

    public List<NewsFeed> GetAll()
        => _context.NewsFeeds.OrderBy(f => f.Name).ToList();

    public List<NewsFeed> GetEnabled()
        => _context.NewsFeeds.Where(f => f.IsEnabled).ToList();

    public NewsFeed? Get(Guid id)
        => _context.NewsFeeds.Where(f => f.Id == id).FirstOrDefault();

    public void Create(NewsFeed feed)
        => _context.Insert(feed);

    public void Update(NewsFeed feed)
        => _context.Update(feed);

    public void Delete(Guid id)
        => _context.NewsFeeds.Where(f => f.Id == id).Delete();

    public void MarkPolled(Guid id, DateTime now, string? error) {
        _context.NewsFeeds
            .Where(f => f.Id == id)
            .Set(f => f.LastPolledAt, now)
            .Set(f => f.LastError, error)
            .Update();
    }
}
