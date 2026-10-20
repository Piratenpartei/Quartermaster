using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Blazor.Components.AssemblyMotions;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionFieldValuesTests {
    private static readonly AssemblyMotionTypeDTO Amendment = new() { Id = Guid.NewGuid(), Code = "SÄA", DefaultText = "Der Bundesparteitag möge beschließen:" };
    private static readonly AssemblyMotionTypeDTO Program = new() { Id = Guid.NewGuid(), Code = "GP", DefaultText = "Der Bundesparteitag möge folgenden Text aufnehmen:" };
    private static readonly AssemblyMotionTypeDTO Other = new() { Id = Guid.NewGuid(), Code = "SO", DefaultText = "" };
    private static readonly List<AssemblyMotionTypeDTO> Types = [Amendment, Program, Other];

    [Test]
    public async Task Choosing_a_type_fills_an_empty_text_with_its_default() {
        var values = new AssemblyMotionFieldValues();

        values.ChangeType(Amendment.Id.ToString(), Types);

        await Assert.That(values.TypeId).IsEqualTo(Amendment.Id.ToString());
        await Assert.That(values.TextMarkdown).IsEqualTo("Der Bundesparteitag möge beschließen:");
    }

    [Test]
    public async Task Switching_type_replaces_an_untouched_default() {
        var values = new AssemblyMotionFieldValues();
        values.ChangeType(Amendment.Id.ToString(), Types);
        values.TextMarkdown += "\r\n";

        values.ChangeType(Program.Id.ToString(), Types);
        var afterProgram = values.TextMarkdown;
        values.ChangeType(Other.Id.ToString(), Types);

        await Assert.That(afterProgram).IsEqualTo("Der Bundesparteitag möge folgenden Text aufnehmen:");
        await Assert.That(values.TextMarkdown).IsEqualTo("");
    }

    [Test]
    public async Task Switching_type_keeps_a_written_text() {
        var values = new AssemblyMotionFieldValues();
        values.ChangeType(Amendment.Id.ToString(), Types);
        values.TextMarkdown = "Der Bundesparteitag möge beschließen:\n\n§ 1 wird gestrichen.";

        values.ChangeType(Program.Id.ToString(), Types);

        await Assert.That(values.TextMarkdown).IsEqualTo("Der Bundesparteitag möge beschließen:\n\n§ 1 wird gestrichen.");
    }
}
