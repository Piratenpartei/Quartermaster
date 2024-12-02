using System;
using System.Security.Cryptography;

namespace Quartermaster.Data;

//https://stackoverflow.com/questions/4181198/how-to-hash-a-password/73125177#73125177
public static class PasswordHashser {
    private const int SaltSize = 16;
    private const int KeySize = 64;
    private const int Iterations = 500_000;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    private const char SegmentDelimiter = ';';

    public static string Hash(string input) {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(input, salt, Iterations, Algorithm, KeySize);

        return string.Join(SegmentDelimiter,
            Convert.ToHexString(hash), Convert.ToHexString(salt), Iterations, Algorithm);
    }

    public static bool Verify(string input, string storedHash) {
        var segments = storedHash.Split(SegmentDelimiter);
        var hash = Convert.FromHexString(segments[0]);
        var salt = Convert.FromHexString(segments[1]);
        var iterations = int.Parse(segments[2]);
        var algorithm = new HashAlgorithmName(segments[3]);

        var inputHash = Rfc2898DeriveBytes.Pbkdf2(input, salt, iterations, algorithm, hash.Length);
        return CryptographicOperations.FixedTimeEquals(inputHash, hash);
    }
}