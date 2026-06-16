namespace UnoGallery.Presentation;

/// <summary>
/// The MVUX model behind <c>MainPage</c>. The gallery itself renders on the Skia
/// surface, so this model is minimal — it just composes the window title from the
/// localized app name and the configured environment.
/// </summary>
public partial record MainModel
{
    /// <summary>Builds the model, composing <see cref="Title"/> from localization + config.</summary>
    public MainModel(
        IStringLocalizer localizer,
        IOptions<AppConfig> appInfo,
        INavigator navigator)
    {
        Title = "Main";
        Title += $" - {localizer["ApplicationName"]}";
        Title += $" - {appInfo?.Value?.Environment}";
    }

    /// <summary>The composed window title.</summary>
    public string? Title { get; }


}
