using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.Options;
using Quartermaster.Data;
using Quartermaster.Data.Options;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Messaging;

public class PdfMessageChannelTests : RepositoryTestBase {
    private DbContext _context = default!;
    private OptionRepository _optionRepo = default!;
    private string _tempDir = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _optionRepo = new OptionRepository(_context, AuditLog);

        _tempDir = Path.Combine(Path.GetTempPath(), $"qm-pdf-test-{Guid.NewGuid():N}");
        SetOutputDir(_tempDir);
    }

    [After(Test)]
    public void Cleanup() {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void SetOutputDir(string path) {
        _context.SystemOptions.Where(o => o.Identifier == "messaging.pdf.output_dir").Delete();
        _context.Insert(new SystemOption { Identifier = "messaging.pdf.output_dir", Value = path });
    }

    private PdfMessageChannel Build() =>
        new(_optionRepo, NullLogger<PdfMessageChannel>.Instance);

    [Test]
    public async Task Writes_pdf_file_to_configured_output_dir() {
        var channel = Build();
        var result = await channel.SendAsync(new ChannelMessage(
            ChannelAddress: "Anna Test\nTeststr. 1\n12345 Teststadt",
            Subject: "Einladung zur Versammlung",
            Body: "Hallo Anna, hiermit laden wir dich ein..."));

        await Assert.That(result.Accepted).IsTrue();
        var files = Directory.GetFiles(_tempDir, "*.pdf");
        await Assert.That(files.Length).IsEqualTo(1);
        var info = new FileInfo(files[0]);
        await Assert.That(info.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Filename_contains_slugified_address() {
        var channel = Build();
        await channel.SendAsync(new ChannelMessage(
            ChannelAddress: "Bob Builder",
            Subject: "Test",
            Body: "Test body"));

        var file = Directory.GetFiles(_tempDir, "*.pdf").Single();
        var name = Path.GetFileName(file);
        await Assert.That(name.Contains("bob_builder")).IsTrue();
        await Assert.That(name.EndsWith(".pdf")).IsTrue();
    }

    [Test]
    public async Task Falls_back_to_default_dir_when_option_blank() {
        SetOutputDir("");
        var channel = Build();
        var result = await channel.SendAsync(new ChannelMessage(
            ChannelAddress: "x",
            Subject: "s",
            Body: "b"));

        await Assert.That(result.Accepted).IsTrue();
        var defaultDir = Path.Combine(AppContext.BaseDirectory, "data", "printouts");
        await Assert.That(Directory.Exists(defaultDir)).IsTrue();
        var files = Directory.GetFiles(defaultDir, "*.pdf");
        await Assert.That(files.Length).IsGreaterThan(0);
        // Cleanup the file we just dropped into the default dir.
        foreach (var f in files)
            File.Delete(f);
    }

    [Test]
    public async Task Empty_address_yields_unaddressed_filename() {
        var channel = Build();
        await channel.SendAsync(new ChannelMessage(
            ChannelAddress: "",
            Subject: "Test",
            Body: "Body"));

        var file = Directory.GetFiles(_tempDir, "*.pdf").Single();
        await Assert.That(Path.GetFileName(file).Contains("unaddressed")).IsTrue();
    }

    [Test]
    public async Task Channel_id_is_pdf() {
        await Assert.That(Build().Id).IsEqualTo("pdf");
    }

    [Test]
    public async Task IsConfigured_is_always_true() {
        await Assert.That(Build().IsConfigured).IsTrue();
    }
}
