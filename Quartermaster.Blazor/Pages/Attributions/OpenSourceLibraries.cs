using System.Collections.Generic;

namespace Quartermaster.Blazor.Pages.Attributions;

/// <summary>
/// The third-party code Quartermaster ships, shown on the sources and licenses page. A test compares the NuGet
/// packages against the project files, so a new package fails the build until it is listed here.
/// </summary>
public static class OpenSourceLibraries {
    /// <summary>Packages that are only used while building or developing and are not shipped.</summary>
    public static readonly IReadOnlyList<string> BuildOnlyPackages = [
        "Grpc.Tools",
        "Microsoft.AspNetCore.Components.WebAssembly.DevServer"
    ];

    public static readonly IReadOnlyList<OpenSourceLibrary> All = [
        new(".NET / ASP.NET Core (Blazor, SignalR)", "MIT", "https://dotnet.microsoft.com", LibraryScope.Server, [
            "Microsoft.AspNetCore.Components.WebAssembly",
            "Microsoft.AspNetCore.Components.WebAssembly.Server",
            "Microsoft.AspNetCore.SignalR.Client",
            "Microsoft.Extensions.Http"
        ]),
        new("AspNetSaml", "Apache-2.0", "https://github.com/jitbit/AspNetSaml", LibraryScope.Server, ["AspNetSaml"]),
        new("Bogus", "MIT", "https://github.com/bchavez/Bogus", LibraryScope.Server, ["Bogus"]),
        new("CsvHelper", "MS-PL OR Apache-2.0", "https://joshclose.github.io/CsvHelper/", LibraryScope.Server, ["CsvHelper"]),
        new("FastEndpoints", "MIT", "https://fast-endpoints.com", LibraryScope.Server, ["FastEndpoints"]),
        new("FluentMigrator", "Apache-2.0", "https://github.com/fluentmigrator/fluentmigrator", LibraryScope.Server, ["FluentMigrator", "FluentMigrator.Runner"]),
        new("FluentValidation", "Apache-2.0", "https://fluentvalidation.net", LibraryScope.Server, ["FluentValidation", "FluentValidation.DependencyInjectionExtensions"]),
        new("Fluid", "MIT", "https://github.com/sebastienros/fluid", LibraryScope.Server, ["Fluid.Core"]),
        new("HtmlSanitizer", "MIT", "https://github.com/mganss/HtmlSanitizer", LibraryScope.Server, ["HtmlSanitizer"]),
        new("LINQ to DB", "MIT", "https://linq2db.github.io", LibraryScope.Server, ["linq2db", "linq2db.AspNet"]),
        new("MailKit / MimeKit", "MIT", "https://www.mimekit.net", LibraryScope.Server, ["MailKit"]),
        new("Markdig", "BSD-2-Clause", "https://github.com/xoofx/markdig", LibraryScope.Server, ["Markdig"]),
        new("Microsoft IdentityModel", "MIT", "https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet", LibraryScope.Server, ["System.IdentityModel.Tokens.Jwt"]),
        new("Mumble.proto", "BSD-3-Clause", "https://www.mumble.info", LibraryScope.Server, []),
        new("MySqlConnector", "MIT", "https://mysqlconnector.net", LibraryScope.Server, ["MySqlConnector"]),
        new("Protocol Buffers", "BSD-3-Clause", "https://github.com/protocolbuffers/protobuf", LibraryScope.Server, ["Google.Protobuf"]),
        new("QuestPDF", "MIT (Community)", "https://www.questpdf.com", LibraryScope.Server, ["QuestPDF"]),
        new("Telegram.Bot", "MIT", "https://github.com/TelegramBots/Telegram.Bot", LibraryScope.Server, ["Telegram.Bot"]),
        new("Bootstrap", "MIT", "https://getbootstrap.com", LibraryScope.Browser, []),
        new("Bootstrap Icons", "MIT", "https://icons.getbootstrap.com", LibraryScope.Browser, []),
        new("CodeMirror 5", "MIT", "https://codemirror.net/5/", LibraryScope.Browser, []),
        new("Yjs / y-protocols / y-codemirror", "MIT", "https://github.com/yjs/yjs", LibraryScope.Browser, [])
    ];
}
