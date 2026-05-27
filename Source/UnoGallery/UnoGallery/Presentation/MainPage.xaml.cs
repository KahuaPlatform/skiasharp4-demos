using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UnoGallery.Audio;
using UnoGallery.Models;
using Windows.Storage.Pickers;
using Windows.System;

namespace UnoGallery.Presentation;

public sealed partial class MainPage : Page
{
    static readonly SolidColorBrush ActiveBrush   = new(Microsoft.UI.Colors.White) { Opacity = 0.20 };
    static readonly SolidColorBrush InactiveBrush = new(Microsoft.UI.Colors.White) { Opacity = 0.07 };

    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Populate the audio-source dropdown from whatever the manager
        // enumerated at startup. Selecting an item triggers a real switch.
        var mgr = AudioSourceManager.Instance;
        AudioSourceCombo.Items.Clear();
        foreach (var info in mgr.Available)
            AudioSourceCombo.Items.Add(info);
        AudioSourceCombo.DisplayMemberPath = nameof(AudioSourceInfo.DisplayName);
        AudioSourceCombo.SelectedItem = mgr.CurrentInfo;
    }

    void OnAudioSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AudioSourceCombo.SelectedItem is AudioSourceInfo info)
            AudioSourceManager.Instance.Use(info);
    }

    void OnEscapeAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Surface.Dismiss();
        args.Handled = true;
    }

    void OnGridClick(object sender, RoutedEventArgs e)     => Switch(LayoutMode.Grid);
    void OnHelixClick(object sender, RoutedEventArgs e)    => Switch(LayoutMode.Helix);
    void OnCarouselClick(object sender, RoutedEventArgs e) => Switch(LayoutMode.Carousel);

    void Switch(LayoutMode mode)
    {
        Surface.SetLayout(mode);
        UpdateLayoutButtons(mode);
    }

    void UpdateLayoutButtons(LayoutMode mode)
    {
        GridBtn.Background     = mode == LayoutMode.Grid     ? ActiveBrush : InactiveBrush;
        HelixBtn.Background    = mode == LayoutMode.Helix    ? ActiveBrush : InactiveBrush;
        CarouselBtn.Background = mode == LayoutMode.Carousel ? ActiveBrush : InactiveBrush;
    }

    void OnDemoClick(object sender, RoutedEventArgs e)
    {
        bool wasOn = Surface.IsDemoMode;
        Surface.SetDemoMode(!wasOn);
        DemoBtn.Content = Surface.IsDemoMode ? "Demo: on" : "Demo: off";
        DemoBtn.Background = Surface.IsDemoMode ? ActiveBrush : InactiveBrush;
    }

    void OnAmbientToggled(object sender, RoutedEventArgs e) =>
        Surface.UpdateSettings(s => s with { EnableAmbientBackground = AmbientToggle.IsOn });

    void OnBloomToggled(object sender, RoutedEventArgs e) =>
        Surface.UpdateSettings(s => s with { EnableBloom = BloomToggle.IsOn });

    void OnVignetteToggled(object sender, RoutedEventArgs e) =>
        Surface.UpdateSettings(s => s with { EnableVignette = VignetteToggle.IsOn });

    void OnGrainToggled(object sender, RoutedEventArgs e) =>
        Surface.UpdateSettings(s => s with { EnableGrain = GrainToggle.IsOn });

    void OnChromaToggled(object sender, RoutedEventArgs e) =>
        Surface.UpdateSettings(s => s with { EnableChromaShift = ChromaToggle.IsOn });

    void OnHoverGlowToggled(object sender, RoutedEventArgs e) =>
        Surface.UpdateSettings(s => s with { EnableHoverGlow = HoverGlowToggle.IsOn });

    void OnDissolveToggled(object sender, RoutedEventArgs e) =>
        Surface.UpdateSettings(s => s with { EnableDissolveTransition = DissolveToggle.IsOn });

    void OnIrisToggled(object sender, RoutedEventArgs e) =>
        Surface.UpdateSettings(s => s with { EnableIrisTransition = IrisToggle.IsOn });

    void OnProfilerToggled(object sender, RoutedEventArgs e) =>
        Surface.UpdateSettings(s => s with { ShowProfiler = ProfilerToggle.IsOn });

    async void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

            // On desktop the picker needs a window handle to anchor against.
            if (Application.Current is App app && app.MainWindow is { } window)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var folder = await picker.PickSingleFolderAsync();
            if (folder is null) return; // user cancelled

            // Pause demo cycle while a real photo set is loading in.
            Surface.SetDemoMode(false);
            DemoBtn.Content = "Demo: off";
            DemoBtn.Background = InactiveBrush;

            await Surface.LoadFromFolderAsync(folder);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OpenFolder] {ex}");
        }
    }
}
