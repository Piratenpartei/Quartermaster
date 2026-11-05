using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Quartermaster.Server.Tests.Texts;

/// <summary>Every German text Quartermaster ships uses one form for person nouns: the generic masculine.</summary>
public class GermanPersonNounTests {
    private static readonly string[] _shippedProjects = ["Quartermaster.Api", "Quartermaster.Blazor", "Quartermaster.Data", "Quartermaster.Server"];
    private static readonly string[] _skippedDirectories = ["bin", "obj", "lib", "node_modules", "Migrations"];

    [Test]
    [Arguments("Neue:r Mitantragsteller:in")]
    [Arguments("jede:r Antragsteller")]
    [Arguments("alle Antragsteller*innen")]
    [Arguments("alle Antragsteller_innen")]
    [Arguments("ein Antragsteller/-in")]
    [Arguments("alle AntragstellerInnen")]
    [Arguments("genug Mitantragstellende")]
    [Arguments("die Kandidierenden")]
    [Arguments("von der antragstellenden Person")]
    public async Task Finds_forms_other_than_the_generic_masculine(string text) {
        await Assert.That(GermanPersonNounScanner.FindViolations(text)).IsNotEmpty();
    }

    [Test]
    [Arguments("Neuer Mitantragsteller")]
    [Arguments("genug Mitantragsteller, der Verfasser und die Kandidaten")]
    [Arguments("Der Vorsitzende und der stellvertretende Vorsitzende")]
    [Arguments("Spenden bis zum Ende senden")]
    [Arguments("Konkurrierende Anträge")]
    [Arguments("Fülle alle Felder oberhalb des Hinweises „Nicht von der unterzeichnenden Person auszufüllen“ aus.")]
    [Arguments("/api/users/sign_in?expires_in=60")]
    public async Task Allows_the_generic_masculine_and_other_words(string text) {
        await Assert.That(GermanPersonNounScanner.FindViolations(text)).IsEmpty();
    }

    /// <summary>Migrations are skipped: they patch earlier texts and have to quote them as they were.</summary>
    [Test]
    public async Task Shipped_texts_use_the_generic_masculine() {
        var root = RepositoryRoot();
        var violations = new List<string>();
        foreach (var file in _shippedProjects.SelectMany(p => ShippedTextFiles(Path.Combine(root, p)))) {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++) {
                foreach (var found in GermanPersonNounScanner.FindViolations(lines[i]))
                    violations.Add($"{Path.GetRelativePath(root, file)}:{i + 1}: {found}");
            }
        }

        if (violations.Count > 0)
            Assert.Fail($"{violations.Count} person nouns break the generic masculine rule in Translations.md:\n{string.Join("\n", violations)}");
        await Assert.That(violations).IsEmpty();
    }

    private static IEnumerable<string> ShippedTextFiles(string directory) {
        foreach (var file in Directory.EnumerateFiles(directory)) {
            if (IsShippedText(file))
                yield return file;
        }
        foreach (var child in Directory.EnumerateDirectories(directory)) {
            if (_skippedDirectories.Contains(Path.GetFileName(child)))
                continue;
            foreach (var file in ShippedTextFiles(child))
                yield return file;
        }
    }

    private static bool IsShippedText(string file) {
        var extension = Path.GetExtension(file);
        return extension == ".cs" || extension == ".razor" || extension == ".md" || Path.GetFileName(file) == "de.json";
    }

    private static string RepositoryRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Quartermaster.sln")))
            directory = directory.Parent;
        if (directory == null)
            throw new InvalidOperationException($"Quartermaster.sln not found above {AppContext.BaseDirectory}");
        return directory.FullName;
    }
}
