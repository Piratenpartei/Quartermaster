using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Quartermaster.Api.Accreditation;

namespace Quartermaster.Server.Accreditation;

// Both generators must produce the same names for the same input, so accounts created here look alike with ones MemberDataImport creates.
/// <summary>The username rule of MemberDataImport's UsernameGenerator, ported.</summary>
public static class AccreditationUsernameGenerator {
    public static string BuildBaseName(string firstName, string lastName, string separator) => UsernameRule.BuildBaseName(firstName, lastName, separator);

    public static string HashSuffix(string memberNumber) {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(memberNumber.Trim()));
        var value = BinaryPrimitives.ReadUInt64BigEndian(hash.AsSpan(0, 8)) % 1_000_000;
        return value.ToString("D6", CultureInfo.InvariantCulture);
    }

    public static bool IsValid(string username) => UsernameRule.IsValid(username);
}
