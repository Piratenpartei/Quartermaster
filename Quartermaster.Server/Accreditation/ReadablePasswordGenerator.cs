using System.Security.Cryptography;

namespace Quartermaster.Server.Accreditation;

public static class ReadablePasswordGenerator {
    public static string Generate() {
        var words = PasswordWords.All;
        var first = words[RandomNumberGenerator.GetInt32(words.Count)];
        var second = words[RandomNumberGenerator.GetInt32(words.Count)];
        var third = words[RandomNumberGenerator.GetInt32(words.Count)];
        var digits = RandomNumberGenerator.GetInt32(10, 100);
        return $"{first}-{second}-{third}-{digits}";
    }
}
