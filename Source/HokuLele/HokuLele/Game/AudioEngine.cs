#if HAS_NAUDIO
using NAudio.Wave;
#endif

namespace HokuLele.Game;

/// <summary>
/// Procedural sound effects for HokuLele, as a static facade. Cross-platform
/// plumbing (NAudio mixer on desktop, JS interop on WASM) lives in
/// <see cref="AudioEngineBase"/>; this type defines the voices and forwards calls.
/// </summary>
public static class AudioEngine
{
    static readonly AudioEngineImpl _impl = new();
    /// <summary>Starts the audio device (desktop). No-op elsewhere.</summary>
    public static void Init()         => _impl.Init();
    /// <summary>Stops and disposes the audio device.</summary>
    public static void Shutdown()     => _impl.Shutdown();
    /// <summary>Player shot blip.</summary>
    public static void PlayShoot()    => _impl.PlayShoot();
    /// <summary>Explosion noise burst (enemy or player death).</summary>
    public static void PlayExplosion() => _impl.PlayExplosion();
    /// <summary>Dive/beam-approach whoosh.</summary>
    public static void PlayDive()     => _impl.PlayDive();

    sealed class AudioEngineImpl : AudioEngineBase
    {
        public void PlayShoot()
        {
#if HAS_NAUDIO
            TryPlay(new ShootSound(SampleRate));
#endif
            WasmPlay("globalThis.hokuLeleAudio && globalThis.hokuLeleAudio.playShoot();");
        }
        public void PlayExplosion()
        {
#if HAS_NAUDIO
            TryPlay(new ExplosionSound(SampleRate));
#endif
            WasmPlay("globalThis.hokuLeleAudio && globalThis.hokuLeleAudio.playExplosion();");
        }
        public void PlayDive()
        {
#if HAS_NAUDIO
            TryPlay(new DiveSound(SampleRate));
#endif
            WasmPlay("globalThis.hokuLeleAudio && globalThis.hokuLeleAudio.playDive();");
        }
    }

#if HAS_NAUDIO
    sealed class ShootSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        int _sample;
        public WaveFormat WaveFormat { get; }
        public ShootSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.08 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float freq = 880f - t * 660f;
                float phase = (_sample * freq / _sampleRate) % 1f;
                float square = phase < 0.5f ? 0.32f : -0.32f;
                float env = 1f - t * 0.5f;
                buffer[offset + i] = square * env;
            }
            return read;
        }
    }

    sealed class ExplosionSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        readonly Random _rng = new();
        int _sample;
        float _filter;
        public WaveFormat WaveFormat { get; }
        public ExplosionSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.35 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _filter = _filter * 0.7f + noise * 0.3f;
                float env = MathF.Exp(-3.2f * t);
                buffer[offset + i] = _filter * env * 0.55f;
            }
            return read;
        }
    }

    sealed class DiveSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        int _sample;
        public WaveFormat WaveFormat { get; }
        public DiveSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.45 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float freq = 500f - t * 400f;
                float phase = (_sample * freq / _sampleRate) % 1f;
                float saw = phase * 2f - 1f;
                float env = MathF.Min(1f, t * 5f) * MathF.Exp(-1.5f * t);
                buffer[offset + i] = saw * env * 0.22f;
            }
            return read;
        }
    }
#endif
}
