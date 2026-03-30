using LinqToDB;
using Quartermaster.Data.Chapters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Options;

public class OptionRepository {
    private readonly DbContext _context;

    public OptionRepository(DbContext context) {
        _context = context;
    }

    public List<OptionDefinition> GetAllDefinitions()
        => _context.OptionDefinitions.OrderBy(d => d.Identifier).ToList();

    public OptionDefinition? GetDefinition(string identifier)
        => _context.OptionDefinitions.Where(d => d.Identifier == identifier).FirstOrDefault();

    public void CreateDefinition(OptionDefinition def) => _context.Insert(def);

    public SystemOption? GetGlobalValue(string identifier)
        => _context.SystemOptions
            .Where(o => o.Identifier == identifier && o.ChapterId == null)
            .FirstOrDefault();

    public SystemOption? GetChapterValue(string identifier, Guid chapterId)
        => _context.SystemOptions
            .Where(o => o.Identifier == identifier && o.ChapterId == chapterId)
            .FirstOrDefault();

    public List<SystemOption> GetAllValues()
        => _context.SystemOptions.ToList();

    public string? ResolveValue(string identifier, Guid? chapterId, ChapterRepository chapterRepo) {
        if (chapterId.HasValue) {
            var chain = chapterRepo.GetAncestorChain(chapterId.Value);
            foreach (var chapter in chain) {
                var chapterValue = GetChapterValue(identifier, chapter.Id);
                if (chapterValue != null)
                    return chapterValue.Value;
            }
        }

        var global = GetGlobalValue(identifier);
        return global?.Value;
    }

    public void SetValue(string identifier, Guid? chapterId, string value) {
        var existing = chapterId.HasValue
            ? GetChapterValue(identifier, chapterId.Value)
            : GetGlobalValue(identifier);

        if (existing != null) {
            _context.SystemOptions
                .Where(o => o.Id == existing.Id)
                .Set(o => o.Value, value)
                .Update();
        } else {
            _context.Insert(new SystemOption {
                Identifier = identifier,
                Value = value,
                ChapterId = chapterId
            });
        }
    }

    public void SupplementDefaults() {
        AddDefinitionIfNotExists("templates.membershipapplication.approved.email",
            "E-Mail: Mitgliedsantrag genehmigt",
            OptionDataType.Template, true,
            "MembershipApplicationDetailDTO,ChapterDTO",
            "Hallo **{{ application.FirstName }}**,\n\ndein Mitgliedsantrag bei der **{{ chapter.Name }}** wurde genehmigt.\n\nWillkommen an Bord!\n");

        AddDefinitionIfNotExists("templates.membershipapplication.rejected.email",
            "E-Mail: Mitgliedsantrag abgelehnt",
            OptionDataType.Template, true,
            "MembershipApplicationDetailDTO,ChapterDTO",
            "Hallo **{{ application.FirstName }}**,\n\nleider wurde dein Mitgliedsantrag bei der **{{ chapter.Name }}** abgelehnt.\n");

        AddDefinitionIfNotExists("templates.dueselection.approved.email",
            "E-Mail: Beitragsminderung genehmigt",
            OptionDataType.Template, true,
            "DueSelectionDetailDTO,ChapterDTO",
            "Hallo **{{ selection.FirstName }}**,\n\ndein Antrag auf Beitragsminderung wurde genehmigt.\n");

        AddDefinitionIfNotExists("templates.dueselection.rejected.email",
            "E-Mail: Beitragsminderung abgelehnt",
            OptionDataType.Template, true,
            "DueSelectionDetailDTO,ChapterDTO",
            "Hallo **{{ selection.FirstName }}**,\n\ndein Antrag auf Beitragsminderung wurde leider abgelehnt.\n");

        AddDefinitionIfNotExists("general.chaptername.display",
            "Anzeigename der Gliederung",
            OptionDataType.String, true, "", "");

        AddDefinitionIfNotExists("general.contact.email",
            "Kontakt E-Mail Adresse",
            OptionDataType.String, true, "", "");

        AddDefinitionIfNotExists("member_import.file_path",
            "Mitgliederimport: Dateipfad",
            OptionDataType.String, false, "", "");

        AddDefinitionIfNotExists("member_import.polling_interval_minutes",
            "Mitgliederimport: Abfrageintervall (Minuten)",
            OptionDataType.Number, false, "", "10");
    }

    private void AddDefinitionIfNotExists(string identifier, string friendlyName,
        OptionDataType dataType, bool isOverridable, string templateModels, string defaultValue) {

        if (GetDefinition(identifier) != null)
            return;

        CreateDefinition(new OptionDefinition {
            Identifier = identifier,
            FriendlyName = friendlyName,
            DataType = dataType,
            IsOverridable = isOverridable,
            TemplateModels = templateModels
        });

        if (!string.IsNullOrEmpty(defaultValue))
            SetValue(identifier, null, defaultValue);
    }
}
