using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Blazor.Pages.Administration.Settings;
using Quartermaster.Data.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Settings;

public class SettingsAssistantCatalogTests : IntegrationTestBase {
    private string[] DefinedOptions() {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<OptionRepository>()
            .GetAllDefinitions()
            .Select(d => d.Identifier)
            .ToArray();
    }

    [Test]
    public async Task Every_option_has_an_assistant_or_is_advanced_only() {
        var covered = SettingsAssistantCatalog.All
            .SelectMany(a => a.AllKeys())
            .Concat(SettingsAssistantCatalog.AdvancedOnlyKeys)
            .ToHashSet();

        var missing = DefinedOptions().Where(k => !covered.Contains(k)).Order().ToList();
        await Assert.That(missing).IsEmpty();
    }

    [Test]
    public async Task Every_catalog_key_is_a_defined_option() {
        var defined = DefinedOptions().ToHashSet();
        var unknown = SettingsAssistantCatalog.All
            .SelectMany(a => a.AllKeys().Concat(a.RequiredKeys).Concat(a.ToggleKeys))
            .Concat(SettingsAssistantCatalog.AdvancedOnlyKeys)
            .Where(k => !defined.Contains(k))
            .Distinct()
            .ToList();

        await Assert.That(unknown).IsEmpty();
    }

    [Test]
    public async Task Advanced_only_keys_have_no_assistant() {
        var inAssistants = SettingsAssistantCatalog.AdvancedOnlyKeys
            .Where(k => SettingsAssistantCatalog.ForKey(k) != null)
            .ToList();

        await Assert.That(inAssistants).IsEmpty();
    }

    [Test]
    public async Task Slugs_are_unique() {
        var duplicates = SettingsAssistantCatalog.All
            .GroupBy(a => a.Slug, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        await Assert.That(duplicates).IsEmpty();
    }
}
