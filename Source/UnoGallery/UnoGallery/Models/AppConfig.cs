namespace UnoGallery.Models;

/// <summary>Bound app configuration (from appsettings), surfaced via <c>IOptions&lt;AppConfig&gt;</c>.</summary>
public record AppConfig
{
    /// <summary>Environment name shown in the window title (e.g. "Development").</summary>
    public string? Environment { get; init; }
}
