using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Quartermaster.Blazor.Pages.Attributions;

namespace Quartermaster.Server.Tests.Attributions;

public class OpenSourceLibrariesTests {
    private static readonly string[] ShippedProjects = [
        "Quartermaster.Api", "Quartermaster.Blazor", "Quartermaster.Data", "Quartermaster.Rendering", "Quartermaster.Server"
    ];

    private static string RepositoryRoot() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Quartermaster.sln")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("Quartermaster.sln not found above the test binaries.");
    }

    private static HashSet<string> ShippedPackages() {
        var root = RepositoryRoot();
        return ShippedProjects
            .SelectMany(p => XDocument.Load(Path.Combine(root, p, $"{p}.csproj")).Descendants("PackageReference"))
            .Select(r => (string?)r.Attribute("Include") ?? "")
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [Test]
    public async Task Every_shipped_package_is_listed() {
        var listed = OpenSourceLibraries.All
            .SelectMany(l => l.Packages)
            .Concat(OpenSourceLibraries.BuildOnlyPackages)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = ShippedPackages().Where(p => !listed.Contains(p)).OrderBy(p => p).ToList();

        await Assert.That(missing).IsEmpty();
    }

    [Test]
    public async Task Listed_packages_are_still_referenced() {
        var shipped = ShippedPackages();

        var stale = OpenSourceLibraries.All
            .SelectMany(l => l.Packages)
            .Concat(OpenSourceLibraries.BuildOnlyPackages)
            .Where(p => !shipped.Contains(p))
            .ToList();

        await Assert.That(stale).IsEmpty();
    }
}
