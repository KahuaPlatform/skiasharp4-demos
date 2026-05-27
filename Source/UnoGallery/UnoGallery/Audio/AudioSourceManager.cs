namespace UnoGallery.Audio;

/// <summary>
/// Process-wide audio source registry. Enumerates available sources at
/// startup (synthesised + any mics NAudio sees), holds the currently
/// active one, and switches between them on demand. WaveformTile reads
/// from <see cref="Current"/>; the settings flyout drives <see cref="Use"/>.
/// </summary>
public sealed class AudioSourceManager : IDisposable
{
    static readonly Lazy<AudioSourceManager> _instance = new(() => new AudioSourceManager());
    public static AudioSourceManager Instance => _instance.Value;

    readonly Lock _lock = new();
    readonly List<AudioSourceInfo> _available = new();
    readonly AudioAnalyzer _analyzer = new();
    IAudioSource _current;

    public event Action? CurrentChanged;

    /// <summary>FFT + beat detector. Refreshed every frame via <see cref="Update"/>.</summary>
    public AudioAnalyzer Analyzer => _analyzer;

    /// <summary>
    /// Run one analysis tick against the currently-selected source.
    /// Cheap (sub-100 µs on desktop); safe to call once per render frame.
    /// </summary>
    public void Update(float wallClockSeconds)
    {
        IAudioSource src;
        lock (_lock) src = _current;
        _analyzer.Update(src, wallClockSeconds);
    }

    AudioSourceManager()
    {
        _available.Add(AudioSourceInfo.Synthesised);

#if HAS_NAUDIO
        try
        {
            _available.AddRange(NAudioMicrophoneSource.EnumerateDevices());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioSourceManager] enum failed: {ex.Message}");
        }
#endif

        // Start on the synthesised source — needs no permission, works everywhere.
        _current = new FakeAudioSource();
        _current.Start();
    }

    public IReadOnlyList<AudioSourceInfo> Available
    {
        get
        {
            lock (_lock) return _available.ToArray();
        }
    }

    public IAudioSource Current
    {
        get { lock (_lock) return _current; }
    }

    public AudioSourceInfo CurrentInfo
    {
        get
        {
            lock (_lock)
            {
                foreach (var info in _available)
                    if (info.DisplayName == _current.Name) return info;
                return AudioSourceInfo.Synthesised;
            }
        }
    }

    /// <summary>Switch to the source identified by <paramref name="info"/>.
    /// Stops the previous source first; falls back to synthesised on error.</summary>
    public void Use(AudioSourceInfo info)
    {
        IAudioSource? old;
        IAudioSource next;
        lock (_lock)
        {
            if (_current.Name == info.DisplayName) return; // already on it

            next = CreateSource(info);
            old = _current;
            _current = next;
        }

        old?.Stop();
        old?.Dispose();
        try
        {
            next.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioSourceManager] failed to start '{info.DisplayName}': {ex.Message}");
            // Fall back to synth.
            var fallback = new FakeAudioSource();
            lock (_lock) _current = fallback;
            fallback.Start();
        }

        CurrentChanged?.Invoke();
    }

    static IAudioSource CreateSource(AudioSourceInfo info)
    {
        if (info.Id == AudioSourceInfo.Synthesised.Id)
            return new FakeAudioSource();

#if HAS_NAUDIO
        if (info.Id.StartsWith("mic:", StringComparison.Ordinal))
        {
            int n = NAudioMicrophoneSource.DeviceNumberFromId(info.Id);
            if (n >= 0)
                return new NAudioMicrophoneSource(n, info.DisplayName);
        }
#endif
        return new FakeAudioSource();
    }

    public void Dispose()
    {
        IAudioSource? c;
        lock (_lock) { c = _current; }
        c?.Dispose();
    }
}
