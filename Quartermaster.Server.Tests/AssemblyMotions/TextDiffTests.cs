using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class TextDiffTests {
    private static string Describe(TextDiffLine line) => $"{line.Kind}:{line.Text}";

    [Test]
    public async Task Identical_texts_are_unchanged() {
        var lines = TextDiff.Lines("a\nb\nc", "a\nb\nc");

        await Assert.That(lines.Select(Describe).ToArray())
            .IsEquivalentTo(new[] { "Unchanged:a", "Unchanged:b", "Unchanged:c" });
    }

    [Test]
    public async Task Inserted_line_is_added() {
        var lines = TextDiff.Lines("a\nc", "a\nb\nc");

        await Assert.That(lines.Select(Describe).ToArray())
            .IsEquivalentTo(new[] { "Unchanged:a", "Added:b", "Unchanged:c" });
    }

    [Test]
    public async Task Removed_line_is_removed() {
        var lines = TextDiff.Lines("a\nb\nc", "a\nc");

        await Assert.That(lines.Select(Describe).ToArray())
            .IsEquivalentTo(new[] { "Unchanged:a", "Removed:b", "Unchanged:c" });
    }

    [Test]
    public async Task Changed_middle_line_yields_removed_then_added() {
        var lines = TextDiff.Lines("a\nb\nc", "a\nx\nc");

        await Assert.That(lines.Select(Describe).ToArray())
            .IsEquivalentTo(new[] { "Unchanged:a", "Removed:b", "Added:x", "Unchanged:c" });
    }

    [Test]
    public async Task Crlf_input_equals_lf_input() {
        var crlf = TextDiff.Lines("a\r\nb\r\nc", "a\r\nx\r\nc");
        var lf = TextDiff.Lines("a\nb\nc", "a\nx\nc");

        await Assert.That(crlf).IsEquivalentTo(lf);
    }

    [Test]
    public async Task Huge_input_falls_back_to_full_replacement() {
        var oldText = string.Join("\n", Enumerable.Range(0, 2500).Select(i => $"old {i}"));
        var newText = string.Join("\n", Enumerable.Range(0, 2500).Select(i => $"old {i}"));

        var lines = TextDiff.Lines(oldText, newText);

        await Assert.That(lines.Count).IsEqualTo(5000);
        await Assert.That(lines.Take(2500).All(l => l.Kind == TextDiffKind.Removed)).IsTrue();
        await Assert.That(lines.Skip(2500).All(l => l.Kind == TextDiffKind.Added)).IsTrue();
    }
}
