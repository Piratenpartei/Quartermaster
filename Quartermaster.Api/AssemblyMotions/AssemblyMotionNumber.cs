namespace Quartermaster.Api.AssemblyMotions;

public static class AssemblyMotionNumber {
    public static string Format(string typeCode, int number) {
        return $"{typeCode}{number:000}";
    }

    /// <summary>The portal-wide number a motion gets on release, for telling motions apart before they have their official number.</summary>
    public static string FormatReference(int referenceNumber) {
        return $"#{referenceNumber}";
    }

    /// <summary>The official number, else the reference number, else null (a draft).</summary>
    public static string? Label(string typeCode, int? number, int? referenceNumber) {
        if (number != null)
            return Format(typeCode, number.Value);
        if (referenceNumber != null)
            return FormatReference(referenceNumber.Value);

        return null;
    }
}
