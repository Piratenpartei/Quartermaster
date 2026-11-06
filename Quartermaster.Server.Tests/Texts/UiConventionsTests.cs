using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Quartermaster.Server.Tests.Texts;

/// <summary>Converted pages follow <c>Quartermaster.Documentation/UiConventions.md</c>: one button rule, no hover zoom, no inline styles.</summary>
public class UiConventionsTests {
    /// <summary>Folders and files relative to <c>Quartermaster.Blazor</c> that follow the conventions.</summary>
    public static readonly string[] ConvertedDirectories = ["."];

    /// <summary>The OBS overlay is styled for a video stream, not for the application.</summary>
    private static readonly string[] _excludedFiles = ["Pages/AssemblyAssistant/AssemblyOverlay.razor"];

    /// <summary>Inline styles that are genuinely needed, as <c>relative/path.razor:style</c>.</summary>
    public static readonly string[] AllowedInlineStyles = [
        "Components/AgendaItemTree.razor:padding-left: @(entry.Depth * 2)rem;",
        "Components/AgendaItemTree.razor:min-width: 28px;",
        "Components/AgendaItemTree.razor:min-width: 3em;",
        "Components/AgendaItemTree.razor:max-width: 160px;",
        "Components/TemplateFieldPalette.razor:top: 1rem;",
        "Components/TemplateFieldPalette.razor:min-height: 0;",
        "Components/TemplateFieldPalette.razor:table-layout: fixed; width: 100%;",
        "Components/TemplateFieldPalette.razor:min-width: 0;",
        "Components/TemplateFieldPalette.razor:width: 32px;",
        "Components/TemplateFieldPalette.razor:font-size: 0.85rem; line-height: 1;",
        "Components/Toaster.razor:white-space: pre-wrap; max-height: 200px; overflow: auto;",
        "Components/Toaster.razor:animation-duration: @(toast.DurationMs.Value)ms;",
        "Components/ConfirmDialog.razor:white-space: pre-line;",
        "Components/LazyTreeNode.razor:width: 28px; text-align: center; display: inline-block;",
        "Components/LazyTreeNode.razor:width: 28px; height: 28px; padding: 0;",
        "Components/Receipts/ReceiptPicker.razor:min-width: 18rem; max-height: 20rem; overflow-y: auto",
        "Components/SupportSignatures/SupportSignatureProgress.razor:width: @Percent(Certified)%",
        "Components/SupportSignatures/SupportSignatureProgress.razor:width: @Share(Collected - Certified)%",
        "Components/SupportSignatures/SupportSignatureProgress.razor:left: @Percent(Target)%",
        "Components/SupportSignatures/CampaignShipmentsTab.razor:width: 6em;",
        "Components/SupportSignatures/CampaignShipmentsTab.razor:max-width: 12em;",
        "Components/Events/EventChecklistEditor.razor:max-width: 200px;",
        "Components/Events/EventChecklistEditor.razor:min-width: 28px;",
        "Components/Events/EventChecklistEditor.razor:width: 1.5em; height: 1.5em;",
        "Components/Events/EventChecklistEditor.razor:width: 1.5em; height: 1.5em; border: 2px solid var(--bs-border-color); border-radius: .25em;",
        "Components/Events/EventChecklistEditor.razor:@(HasNoEmailContent(item.Configuration) ? ",
        "Components/Events/EventChecklistEditor.razor:white-space: pre-line;",
        "Components/Events/EventChecklistEditor.razor:max-height: 320px; overflow-y: auto;",
        "Components/AssemblyMotions/AssemblyMotionRankingTable.razor:width: 5rem;",
        "Components/AssemblyMotions/AssemblyMotionRankingTable.razor:width: 8rem;",
        "Components/AssemblyMotions/AssemblyMotionPortalSettings.razor:width: 5rem;",
        "Components/AssemblyAssistant/PollPanel.razor:width: 5rem;",
        "Components/AssemblyAssistant/PollPanel.razor:min-width: 12rem; width: auto;",
        "Components/AssemblyAssistant/PollPanel.razor:width: 4.5rem;",
        "Components/Inputs/AdminDivisionChipsInput.razor:font-size: 0.6rem;",
        "Components/Inputs/StringChipsInput.razor:min-height: calc(2.25rem + 2px);",
        "Components/Inputs/StringChipsInput.razor:font-size: 0.6rem;",
        "Components/Inputs/StringChipsInput.razor:outline: none; min-width: 100px;",
        "Components/Inputs/SearchDropdown.razor:@WrapperStyle",
        "Components/Inputs/SearchDropdown.razor:@InputStyle",
        "Components/Inputs/SearchDropdown.razor:z-index: 1000; max-height: 250px; overflow-y: auto; top: 100%;",
        "Components/Inputs/MunicipalityPicker.razor:width: 10em;",
        "Components/Inputs/MunicipalityPicker.razor:max-height: 400px; overflow-y: auto;",
        "Components/Inputs/CodeMirrorEditorWithPreview.razor:background-color: @peer.Color",
        "Components/Forwarding/ForwardingTargetEditor.razor:max-width: 7rem",
        "Pages/Forms/TravelExpenses/TravelExpensePayoutStep.razor:max-width: 14rem",
        "Pages/MembershipApplication/MembershipAddressStep.razor:max-width: 10em;",
        "Pages/MembershipApplication/MembershipAddressStep.razor:width: 8em;",
        "Pages/DueSelector/MonthlyIncomeGroupInput.razor:min-width: 10em;",
        "Pages/Administration/AdministrativeDivisionSearch.razor:max-width: 300px;",
        "Pages/Administration/AdministrativeDivisionTree.razor:max-width: 200px;",
        "Pages/Administration/AssemblyMotions/AssemblyMotionCommissionDetail.razor:min-width: 16rem;",
        "Pages/Administration/AssemblyMotions/AssemblyMotionResults.razor:min-width: 14rem;",
        "Pages/Administration/ChapterOfficerList.razor:max-width: 400px;",
        "Pages/Administration/DueSelectionAdmin.razor:max-width: 250px;",
        "Pages/Administration/EventCreateFromTemplate.razor:max-width: 300px;",
        "Pages/Administration/EventDetail.razor:max-width: 200px;",
        "Pages/Administration/Forwarding/ForwardingCases.razor:max-width: 16rem",
        "Pages/Administration/ImportStatus.razor:max-height: 200px; overflow-y: auto;",
        "Pages/Administration/Mailings/MailingDetail.razor:@(SendDisabled ? ",
        "Pages/Administration/Mailings/MailingDetail.razor:max-height: 320px; overflow-y: auto;",
        "Pages/Administration/MeetingDetail.razor:max-width: 200px;",
        "Pages/Administration/MeetingList.razor:min-width: 220px;",
        "Pages/Administration/MeetingLive.razor:min-width: 0;",
        "Pages/Administration/MeetingLive.razor:min-width: 22px;",
        "Pages/Administration/MeetingLive.razor:padding-left: @(entry.Depth * 1.5 + 0.5)rem; cursor: pointer;",
        "Pages/Administration/MeetingLive.razor:width: 20rem; min-width: 20rem;",
        "Pages/Administration/MemberDetail.razor:max-width: 200px;",
        "Pages/Administration/MemberList.razor:max-width: 400px;",
        "Pages/Administration/MembershipApplicationDetail.razor:max-width: 520px;",
        "Pages/Administration/MotionDetail.razor:width:160px",
        "Pages/Administration/OptionDetail.razor:max-width: 300px;",
        "Pages/Administration/OptionList.razor:max-width: 400px;",
        "Pages/Administration/Roles/RoleEdit.razor:max-height: 400px; overflow-y: auto;",
        "Pages/Administration/Roles/RoleList.razor:max-height: 300px; overflow-y: auto;",
        "Pages/Administration/Settings/PublicFormsSetup.razor:max-width: 10rem;",
        "Pages/Administration/Settings/PublicFormsSetup.razor:min-width: 15rem;",
        "Pages/Administration/SupportSignatures/CertificationOffices.razor:font-size: 0.6rem;",
        "Pages/Administration/TravelExpenseRules/TravelExpenseRuleAdmin.razor:min-width: 18rem",
        "Pages/Administration/UserDetail.razor:font-size: 0.5em;"
    ];

    /// <summary>The dark and light button and badge variants keep their colour in both themes, so they vanish or glare in one of them.</summary>
    private static readonly string[] _forbiddenClasses = [
        "btn-hover-scale1", "btn-outline-primary", "btn-outline-dark", "btn-outline-light", "btn-dark", "btn-light", "text-bg-light", "text-bg-dark"
    ];

    private static readonly Regex _iconClass = new(@"(?<![\w-])bi-[a-z0-9]+(?:-[a-z0-9]+)*(?![\w{@-])");
    private static readonly Regex _iconParameter = new(@"\bIcon=""([a-z0-9-]+)""");
    private static readonly Regex _iconDefinition = new(@"\.(bi-[a-z0-9-]+)::before");

    [Test]
    public async Task Converted_pages_follow_the_ui_conventions() {
        var blazor = Path.Combine(RepositoryRoot(), "Quartermaster.Blazor");
        var violations = new List<string>();
        var files = ConvertedDirectories
            .SelectMany(d => RazorFilesUnder(Path.Combine(blazor, d)))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => !_excludedFiles.Contains(Path.GetRelativePath(blazor, f).Replace('\\', '/')))
            .Distinct();
        foreach (var file in files) {
            var relative = Path.GetRelativePath(blazor, file);
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
                violations.AddRange(ViolationsIn(relative, lines[i]).Select(v => $"{relative}:{i + 1}: {v}"));
        }

        if (violations.Count > 0)
            Assert.Fail($"{violations.Count} places break UiConventions.md:\n{string.Join("\n", violations)}");
        await Assert.That(violations).IsEmpty();
    }

    /// <summary>An icon name Bootstrap Icons does not know renders nothing and leaves an empty slot.</summary>
    [Test]
    public async Task Every_icon_name_exists_in_bootstrap_icons() {
        var blazor = Path.Combine(RepositoryRoot(), "Quartermaster.Blazor");
        var known = KnownIconNames(blazor);
        var missing = new List<string>();
        foreach (var file in SourceFilesUnder(blazor)) {
            var relative = Path.GetRelativePath(blazor, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
                missing.AddRange(IconNamesIn(lines[i]).Where(n => !known.Contains(n)).Select(n => $"{relative}:{i + 1}: {n}"));
        }

        if (missing.Count > 0)
            Assert.Fail($"{missing.Count} icon names are not in Bootstrap Icons:\n{string.Join("\n", missing)}");
        await Assert.That(missing).IsEmpty();
    }

    private static HashSet<string> KnownIconNames(string blazor) {
        var stylesheets = Directory.GetFiles(Path.Combine(blazor, "wwwroot", "icons"), "bootstrap-icons.css", SearchOption.AllDirectories);
        if (stylesheets.Length != 1)
            throw new InvalidOperationException($"Expected one bootstrap-icons.css under wwwroot/icons, found {stylesheets.Length}");
        return _iconDefinition.Matches(File.ReadAllText(stylesheets[0])).Select(m => m.Groups[1].Value).ToHashSet();
    }

    /// <summary>Class names such as <c>bi bi-save</c> or <c>"bi-sliders"</c>, and component parameters such as <c>Icon="save"</c> that add the prefix themselves.</summary>
    private static IEnumerable<string> IconNamesIn(string line) {
        foreach (Match match in _iconClass.Matches(line))
            yield return match.Value;
        foreach (Match match in _iconParameter.Matches(line)) {
            var value = match.Groups[1].Value;
            yield return value.StartsWith("bi-", StringComparison.Ordinal) ? value : $"bi-{value}";
        }
    }

    private static IEnumerable<string> SourceFilesUnder(string blazor) {
        return Directory.EnumerateFiles(blazor, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }

    private static IEnumerable<string> ViolationsIn(string relativeFile, string line) {
        foreach (var forbidden in _forbiddenClasses.Where(line.Contains))
            yield return forbidden;

        var start = line.IndexOf("style=\"", StringComparison.Ordinal);
        while (start >= 0) {
            var end = line.IndexOf('"', start + 7);
            var style = end < 0 ? line[start..] : line[(start + 7)..end];
            if (!AllowedInlineStyles.Contains($"{relativeFile.Replace('\\', '/')}:{style}"))
                yield return $"style=\"{style}\"";
            start = end < 0 ? -1 : line.IndexOf("style=\"", end, StringComparison.Ordinal);
        }
    }

    private static IEnumerable<string> RazorFilesUnder(string path) {
        if (File.Exists(path))
            return [path];
        return Directory.EnumerateFiles(path, "*.razor", SearchOption.AllDirectories);
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
