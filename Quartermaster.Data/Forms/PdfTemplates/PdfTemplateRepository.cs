using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.Forms.PdfTemplates;

public class PdfTemplateRepository {
    private readonly DbContext _context;

    public PdfTemplateRepository(DbContext context) {
        _context = context;
    }

    public PdfTemplate? GetByKey(string key)
        => _context.PdfTemplates.Where(t => t.Key == key).FirstOrDefault();

    public List<PdfTemplate> GetAll()
        => _context.PdfTemplates.OrderBy(t => t.Key).ToList();

    public void Create(PdfTemplate template)
        => _context.Insert(template);

    public void UpdateBody(string key, string body, DateTime updatedAt, Guid? updatedByUserId) {
        _context.PdfTemplates
            .Where(t => t.Key == key)
            .Set(t => t.Body, body)
            .Set(t => t.UpdatedAt, updatedAt)
            .Set(t => t.UpdatedByUserId, updatedByUserId)
            .Update();
    }

    /// <summary>Inserts the default body for <paramref name="key"/> only when no row exists yet. Used by startup seeding so admin overrides are never clobbered.</summary>
    public void EnsureSeeded(string key, string defaultBody, DateTime now) {
        if (_context.PdfTemplates.Any(t => t.Key == key)) {
            return;
        }
        _context.Insert(new PdfTemplate {
            Id = Guid.NewGuid(),
            Key = key,
            Body = defaultBody,
            UpdatedAt = now,
            UpdatedByUserId = null
        });
    }
}
