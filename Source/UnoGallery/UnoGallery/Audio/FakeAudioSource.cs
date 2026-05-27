using System.Diagnostics;

namespace UnoGallery.Audio;

/// <summary>
/// Synthesised audio — two drifting sine carriers, a noise dash, and a
/// breathing envelope. No microphone permission needed. Used as the
/// default source so the Waveform tile animates out of the box even when
/// no real input is available.
///
/// Drives <see cref="CopyLatest"/> directly from the requesting thread —
/// no background timer, no buffer push. Cheap.
/// </summary>
public sealed class FakeAudioSource : IAudioSource
{
    readonly Stopwatch _clock = Stopwatch.StartNew();

    public string Name => AudioSourceInfo.Synthesised.DisplayName;
    public bool IsRunning => true;

    public void Start() { /* always running */ }
    public void Stop() { /* no-op */ }

    public void CopyLatest(Span<float> dest)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        for (int i = 0; i < dest.Length; i++)
        {
            float x = i / (float)dest.Length;
            float envelope = 0.55f + 0.45f * MathF.Sin(t * 1.4f + x * 3.1f);
            float carrier  = MathF.Sin(t * 6.0f  + x * 28f);
            float overtone = 0.40f * MathF.Sin(t * 11.0f + x * 64f + 0.7f);
            float noise    = (Hash(t * 80f + i * 7.3f) - 0.5f) * 0.22f;
            dest[i] = (carrier + overtone + noise) * envelope;
        }
    }

    public void Dispose() { /* no resources held */ }

    static float Hash(float x)
    {
        float s = MathF.Sin(x * 12.9898f) * 43758.5453f;
        return s - MathF.Floor(s);
    }
}
