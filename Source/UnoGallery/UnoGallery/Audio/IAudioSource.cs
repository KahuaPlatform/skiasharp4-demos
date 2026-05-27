namespace UnoGallery.Audio;

/// <summary>
/// A live stream of audio samples normalised to the [-1, 1] range.
/// Implementations push samples in (from a mic callback, a synth tick, etc.)
/// and the UI thread reads the latest window via <see cref="CopyLatest"/>.
/// </summary>
public interface IAudioSource : IDisposable
{
    string Name { get; }
    bool IsRunning { get; }

    void Start();
    void Stop();

    /// <summary>
    /// Copy the most recent <paramref name="destination"/>.Length samples into
    /// the buffer. Older positions get older audio; newest sample is at
    /// <c>destination[^1]</c>. May write zeros if no audio has been captured
    /// yet. Safe to call from the UI thread regardless of how the source
    /// fills its buffer internally.
    /// </summary>
    void CopyLatest(Span<float> destination);
}

// Uno's IKeyEquatable generator tries to generate partial-class infrastructure
// for any record with an Id-like property — it only handles record classes,
// so opt this struct out explicitly.
[Uno.Extensions.Equality.ImplicitKeys(IsEnabled = false)]
public readonly record struct AudioSourceInfo(string Id, string DisplayName)
{
    public static readonly AudioSourceInfo Synthesised = new("synth", "Synthesised (no mic)");
}
