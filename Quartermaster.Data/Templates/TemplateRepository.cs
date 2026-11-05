using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Data.Templates;

public class TemplateRepository {
    private readonly DbContext _context;

    public TemplateRepository(DbContext context) {
        _context = context;
    }

    public Template? Get(Guid id)
        => _context.Templates.Where(t => t.Id == id && t.DeletedAt == null).FirstOrDefault();

    public List<Template> GetAll()
        => _context.Templates.Where(t => t.DeletedAt == null).OrderBy(t => t.DisplayName).ToList();

    public List<Template> GetOverridesForBaseId(Guid baseId)
        => _context.Templates
            .Where(t => t.BaseTemplateId == baseId && t.DeletedAt == null)
            .ToList();

    public Template? Resolve(string identifier, Guid? chapterId, ChapterRepository chapterRepo) {
        if (string.IsNullOrEmpty(identifier))
            return null;

        var baseRow = _context.Templates
            .Where(t => t.Identifier == identifier && t.ChapterId == null && t.DeletedAt == null)
            .FirstOrDefault();
        if (baseRow == null)
            return null;

        return ResolveById(baseRow.Id, chapterId, chapterRepo) ?? baseRow;
    }

    public Template? ResolveById(Guid baseId, Guid? chapterId, ChapterRepository chapterRepo) {
        var baseRow = Get(baseId);
        if (baseRow == null)
            return null;

        if (!chapterId.HasValue)
            return baseRow;

        var overrides = GetOverridesForBaseId(baseId);
        if (overrides.Count == 0)
            return baseRow;

        foreach (var cid in chapterRepo.GetAncestorChainIds(chapterId.Value)) {
            var match = overrides.FirstOrDefault(t => t.ChapterId == cid);
            if (match != null)
                return match;
        }
        return baseRow;
    }

    public Template? GetSystemBase(string identifier)
        => _context.Templates
            .Where(t => t.Identifier == identifier && t.ChapterId == null && t.DeletedAt == null)
            .FirstOrDefault();

    public void Create(Template template) {
        if (template.Id == Guid.Empty)
            template.Id = Guid.NewGuid();
        if (template.CreatedAt == default)
            template.CreatedAt = DateTime.UtcNow;
        _context.Insert(template);
    }

    public void Update(Template template) {
        _context.Templates
            .Where(t => t.Id == template.Id)
            .Set(t => t.DisplayName, template.DisplayName)
            .Set(t => t.Subject, template.Subject)
            .Set(t => t.Body, template.Body)
            .Set(t => t.AllowsMemberFields, template.AllowsMemberFields)
            .Set(t => t.AllowsEventFields, template.AllowsEventFields)
            .Set(t => t.AllowsChapterFields, template.AllowsChapterFields)
            .Set(t => t.BaseTemplateId, template.BaseTemplateId)
            .Set(t => t.RenderMode, template.RenderMode)
            .Update();
    }

    public void SoftDelete(Guid id) {
        _context.Templates
            .Where(t => t.Id == id)
            .Set(t => t.DeletedAt, DateTime.UtcNow)
            .Update();
    }

    /// <summary>
    /// Creates missing system templates and brings every unedited one to its current seed text; a row counts as unedited while its
    /// text still matches the seed hash it was written with. Edited rows and chapter copies are never touched.
    /// </summary>
    public void SupplementDefaults() {
        foreach (var seed in SystemTemplateSeeds.All) {
            var seedHash = TemplateSeedHash.Compute(seed.DisplayName, seed.Subject, seed.Body);
            var existing = GetSystemBase(seed.Identifier);
            if (existing == null) {
                Create(new Template {
                    Identifier = seed.Identifier,
                    DisplayName = seed.DisplayName,
                    IsSystem = true,
                    ChapterId = null,
                    Subject = seed.Subject,
                    Body = seed.Body,
                    AllowsMemberFields = seed.AllowsMemberFields,
                    AllowsEventFields = seed.AllowsEventFields,
                    AllowsChapterFields = seed.AllowsChapterFields,
                    RenderMode = seed.RenderMode,
                    SeedHash = seedHash
                });
                continue;
            }

            var storedHash = TemplateSeedHash.Compute(existing);
            if (storedHash == seedHash) {
                if (existing.SeedHash != seedHash)
                    SetSeedHash(existing.Id, seedHash);
                continue;
            }
            if (IsUnedited(existing, storedHash))
                ApplySeed(existing.Id, seed, seedHash);
        }
    }

    private static bool IsUnedited(Template existing, string storedHash) {
        if (existing.SeedHash != null)
            return existing.SeedHash == storedHash;
        return SystemTemplateLegacySeeds.Contains(existing.Identifier!, storedHash);
    }

    private void SetSeedHash(Guid id, string seedHash) {
        _context.Templates
            .Where(t => t.Id == id)
            .Set(t => t.SeedHash, seedHash)
            .Update();
    }

    private void ApplySeed(Guid id, SystemTemplateSeed seed, string seedHash) {
        _context.Templates
            .Where(t => t.Id == id)
            .Set(t => t.DisplayName, seed.DisplayName)
            .Set(t => t.Subject, seed.Subject)
            .Set(t => t.Body, seed.Body)
            .Set(t => t.SeedHash, seedHash)
            .Update();
    }
}
