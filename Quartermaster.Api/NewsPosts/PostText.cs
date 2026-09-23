using System.Globalization;

namespace Quartermaster.Api.NewsPosts;

/// <summary>The single length rule for post text, shared by the Blazor counter, the generator and the publish endpoint.
/// Counts text elements so an emoji or a letter with combining marks counts once.</summary>
public static class PostText {
    public const int MaxLength = 280;

    public static int Length(string text) => new StringInfo(text).LengthInTextElements;

    public static bool IsValid(string? text) {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return Length(text) <= MaxLength;
    }
}
