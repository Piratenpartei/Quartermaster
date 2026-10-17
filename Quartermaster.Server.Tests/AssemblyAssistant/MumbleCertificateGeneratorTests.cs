using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Quartermaster.Server.AssemblyAssistant.Mumble;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class MumbleCertificateGeneratorTests {
    [Test]
    public async Task Certificate_loads_with_password_and_has_private_key() {
        var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

        var generated = MumbleCertificateGenerator.Create("Versammlungsassistent", now);
        using var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(generated.Pkcs12Base64), generated.Password);

        await Assert.That(certificate.HasPrivateKey).IsTrue();
        await Assert.That(certificate.Subject).IsEqualTo("CN=Versammlungsassistent");
        await Assert.That(certificate.NotAfter.ToUniversalTime()).IsGreaterThan(now.AddYears(9));
        await Assert.That(MumbleCertificateGenerator.MumbleHash(certificate).Length).IsEqualTo(40);
    }

    [Test]
    public async Task Two_certificates_differ() {
        var now = DateTime.UtcNow;

        var first = MumbleCertificateGenerator.Create("Bot", now);
        var second = MumbleCertificateGenerator.Create("Bot", now);

        await Assert.That(first.Pkcs12Base64).IsNotEqualTo(second.Pkcs12Base64);
        await Assert.That(first.Password).IsNotEqualTo(second.Password);
    }
}
