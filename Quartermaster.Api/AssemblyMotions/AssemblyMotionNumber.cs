namespace Quartermaster.Api.AssemblyMotions;

public static class AssemblyMotionNumber {
    public static string Format(string typeCode, int number) => $"{typeCode}{number:000}";
}
