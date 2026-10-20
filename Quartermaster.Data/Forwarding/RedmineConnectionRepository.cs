using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.Forwarding;

public class RedmineConnectionRepository {
    private readonly DbContext _context;

    public RedmineConnectionRepository(DbContext context) {
        _context = context;
    }

    public RedmineConnection? Get(Guid chapterId) {
        return _context.RedmineConnections.FirstOrDefault(c => c.ChapterId == chapterId);
    }

    public List<RedmineConnection> List() {
        return _context.RedmineConnections.ToList();
    }

    /// <summary>Creates or updates the chapter's connection; a null <paramref name="apiKey"/> keeps the stored key.</summary>
    public void Upsert(Guid chapterId, string baseUrl, string? apiKey, DateTime nowUtc) {
        var existing = Get(chapterId);
        if (existing == null) {
            _context.Insert(new RedmineConnection {
                ChapterId = chapterId,
                BaseUrl = baseUrl,
                ApiKey = apiKey ?? "",
                UpdatedAt = nowUtc
            });
            return;
        }

        _context.RedmineConnections
            .Where(c => c.ChapterId == chapterId)
            .Set(c => c.BaseUrl, baseUrl)
            .Set(c => c.ApiKey, apiKey ?? existing.ApiKey)
            .Set(c => c.UpdatedAt, nowUtc)
            .Update();
    }

    public bool Delete(Guid chapterId) {
        return _context.RedmineConnections.Where(c => c.ChapterId == chapterId).Delete() > 0;
    }
}
