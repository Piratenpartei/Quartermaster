using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.Options;
using Quartermaster.Blazor.Pages.Administration.Settings;

namespace Quartermaster.Server.Tests.Settings;

public class SettingsStatusResolverTests {
    private static readonly SettingsAssistant Required = new("Test", SettingsGroup.Basics, "bi-x", "title", "description") {
        RequiredKeys = ["a", "b"]
    };

    private static readonly SettingsAssistant Toggle = new("Toggle", SettingsGroup.Basics, "bi-x", "title", "description") {
        ToggleKeys = ["on"]
    };

    private static Dictionary<string, OptionDefinitionDTO> Options(params (string Key, string Value)[] values) {
        return values.ToDictionary(v => v.Key, v => new OptionDefinitionDTO { Identifier = v.Key, GlobalValue = v.Value });
    }

    [Test]
    public async Task All_required_values_mean_configured() {
        var status = SettingsStatusResolver.Resolve(Required, Options(("a", "x"), ("b", "y")));
        await Assert.That(status).IsEqualTo(SettingsStatus.Configured);
    }

    [Test]
    public async Task Some_required_values_mean_partial() {
        var status = SettingsStatusResolver.Resolve(Required, Options(("a", "x"), ("b", " ")));
        await Assert.That(status).IsEqualTo(SettingsStatus.Partial);
    }

    [Test]
    public async Task No_required_values_mean_not_configured() {
        var status = SettingsStatusResolver.Resolve(Required, Options());
        await Assert.That(status).IsEqualTo(SettingsStatus.NotConfigured);
    }

    [Test]
    public async Task A_chapter_override_switches_a_toggle_on() {
        var options = Options(("on", "false"));
        options["on"].Overrides.Add(new OptionOverrideDTO { ChapterId = Guid.NewGuid(), Value = "true" });

        var status = SettingsStatusResolver.Resolve(Toggle, options);
        await Assert.That(status).IsEqualTo(SettingsStatus.On);
    }

    [Test]
    public async Task A_false_toggle_is_off() {
        var status = SettingsStatusResolver.Resolve(Toggle, Options(("on", "false")));
        await Assert.That(status).IsEqualTo(SettingsStatus.Off);
    }

    [Test]
    public async Task Without_required_or_toggle_keys_there_is_no_status() {
        var assistant = new SettingsAssistant("None", SettingsGroup.Basics, "bi-x", "title", "description");
        await Assert.That(SettingsStatusResolver.Resolve(assistant, Options())).IsEqualTo(SettingsStatus.None);
    }
}
