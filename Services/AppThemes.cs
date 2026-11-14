namespace jtimer.Services;

public sealed record AppThemeOption(string Id, string Name);

/// <summary>
/// Accent palettes offered on the Configuration screen. The ids match the
/// <c>data-theme</c> values in <c>wwwroot/css/app.css</c>.
/// </summary>
public static class AppThemes
{
    public const string DefaultId = "purple";

    public static IReadOnlyList<AppThemeOption> All { get; } =
    [
        new("purple", "Purple"),
        new("ocean", "Ocean"),
        new("teal", "Teal"),
        new("green", "Green"),
        new("amber", "Amber"),
        new("rose", "Rose")
    ];

    public static string Normalize(string? id) =>
        All.Any(theme => theme.Id == id) ? id! : DefaultId;
}
