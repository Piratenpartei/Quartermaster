using System.Threading.Tasks;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Layout;

public partial class LanguageSwitcher {
    private async Task SwitchTo(string lang) => await Language.SetLanguageAsync(lang);

    private static string Label(string lang) => lang switch {
        "de" => "Deutsch",
        "en" => "English",
        _ => lang
    };
}
