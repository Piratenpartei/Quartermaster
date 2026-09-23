using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.NewsPosts;

public class PostPublicationRepository {
    private readonly DbContext _context;

    public PostPublicationRepository(DbContext context) {
        _context = context;
    }

    public void Create(PostPublication publication)
        => _context.Insert(publication);

    public List<PostPublication> GetRecent(int limit)
        => _context.PostPublications
            .OrderByDescending(p => p.ApprovedAt)
            .Take(limit)
            .ToList();
}
