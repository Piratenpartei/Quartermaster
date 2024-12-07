namespace Quartermaster.Blazor.Components;

public enum Theme {
    Dark,
    Light
}

public static class ThemeExtensions {
    public static string ToHtmlString(this Theme theme) => theme.ToString().ToLower();
}