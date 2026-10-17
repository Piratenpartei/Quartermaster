using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

public record MumbleGeneratedCertificate(string Pkcs12Base64, string Password);

/// <summary>
/// Creates the bot's long-lived self-signed client certificate. Mumble identifies a registered user by the certificate hash,
/// so a new certificate makes the bot a new user on the server.
/// </summary>
public static class MumbleCertificateGenerator {
    public static MumbleGeneratedCertificate Create(string username, DateTime nowUtc) {
        using var rsa = RSA.Create(2048);
        var subjectBuilder = new X500DistinguishedNameBuilder();
        subjectBuilder.AddCommonName(username);
        var subject = subjectBuilder.Build();
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.2")], false));

        using var certificate = request.CreateSelfSigned(nowUtc.AddDays(-1), nowUtc.AddYears(10));
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        var pkcs12 = certificate.Export(X509ContentType.Pkcs12, password);

        return new MumbleGeneratedCertificate(Convert.ToBase64String(pkcs12), password);
    }

    /// <summary>The SHA-1 hex hash Mumble shows for a certificate (UserState.hash).</summary>
    public static string MumbleHash(X509Certificate2 certificate) {
        return Convert.ToHexString(certificate.GetCertHash(HashAlgorithmName.SHA1)).ToLowerInvariant();
    }
}
