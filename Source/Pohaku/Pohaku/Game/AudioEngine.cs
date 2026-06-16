#if HAS_NAUDIO
using NAudio.Wave;
#endif

namespace Pohaku.Game;

/// <summary>
/// Procedural sound effects for Pohaku, as a static facade. Cross-platform
/// plumbing (NAudio mixer on desktop, JS interop on WASM) lives in
/// <see cref="AudioEngineBase"/>; this type defines the voices and forwards calls.
/// </summary>
public static class AudioEngine
{
    static readonly AudioEngineImpl _impl = new();
    /// <summary>Starts the audio device (desktop). No-op elsewhere.</summary>
    public static void Init()              => _impl.Init();
    /// <summary>Stops and disposes the audio device.</summary>
    public static void Shutdown()          => _impl.Shutdown();
    /// <summary>Player shot blip.</summary>
    public static void PlayShoot()         => _impl.PlayShoot();
    /// <summary>Asteroid/ship explosion noise burst.</summary>
    public static void PlayExplosion()     => _impl.PlayExplosion();
    /// <summary>Hyperspace teleport warble.</summary>
    public static void PlayHyperspace()    => _impl.PlayHyperspace();
    /// <summary>Begins the looping thrust rumble.</summary>
    public static void StartThrust()       => _impl.StartThrust();
    /// <summary>Ends the thrust rumble.</summary>
    public static void StopThrust()        => _impl.StopThrust();
    /// <summary>Begins the looping saucer warble (large vs small pitch).</summary>
    public static void StartSaucer(bool large) => _impl.StartSaucer(large);
    /// <summary>Ends the saucer warble.</summary>
    public static void StopSaucer()        => _impl.StopSaucer();

    sealed class AudioEngineImpl : AudioEngineBase
    {
#if HAS_NAUDIO
        ThrustLoop? _thrust;
        SaucerHum? _saucer;
#endif

        public void PlayShoot()
        {
#if HAS_NAUDIO
            TryPlay(new ShootSound(SampleRate));
#endif
            WasmPlay("globalThis.pohakuAudio && globalThis.pohakuAudio.playShoot();");
        }
        public void PlayExplosion()
        {
#if HAS_NAUDIO
            TryPlay(new ExplosionSound(SampleRate));
#endif
            WasmPlay("globalThis.pohakuAudio && globalThis.pohakuAudio.playExplosion();");
        }
        public void PlayHyperspace()
        {
#if HAS_NAUDIO
            TryPlay(new HyperspaceSound(SampleRate));
#endif
            WasmPlay("globalThis.pohakuAudio && globalThis.pohakuAudio.playHyperspace();");
        }
        public void StartThrust()
        {
#if HAS_NAUDIO
            if (_thrust != null) return;
            _thrust = new ThrustLoop(SampleRate);
            TryPlay(_thrust);
#endif
            WasmPlay("globalThis.pohakuAudio && globalThis.pohakuAudio.startThrust();");
        }
        public void StopThrust()
        {
#if HAS_NAUDIO
            _thrust?.Stop();
            _thrust = null;
#endif
            WasmPlay("globalThis.pohakuAudio && globalThis.pohakuAudio.stopThrust();");
        }
        public void StartSaucer(bool large)
        {
#if HAS_NAUDIO
            if (_saucer != null) return;
            _saucer = new SaucerHum(SampleRate, large);
            TryPlay(_saucer);
#endif
            WasmPlay($"globalThis.pohakuAudio && globalThis.pohakuAudio.startSaucer({(large ? "true" : "false")});");
        }
        public void StopSaucer()
        {
#if HAS_NAUDIO
            _saucer?.Stop();
            _saucer = null;
#endif
            WasmPlay("globalThis.pohakuAudio && globalThis.pohakuAudio.stopSaucer();");
        }
    }

#if HAS_NAUDIO
    // --- One-shot voices ---

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
            _totalSamples = (int)(0.4 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _filter = _filter * 0.65f + noise * 0.35f;
                float env = MathF.Exp(-3.0f * t);
                buffer[offset + i] = _filter * env * 0.6f;
            }
            return read;
        }
    }

    sealed class HyperspaceSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        readonly Random _rng = new();
        int _sample;
        public WaveFormat WaveFormat { get; }
        public HyperspaceSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.32 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float freq = 80f + (1500f - 80f) * MathF.Pow(1f - t, 2.4f);
                float phase = (_sample * freq / _sampleRate) % 1f;
                float saw = phase * 2f - 1f;
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0) * 0.2f;
                float env = MathF.Min(1f, t * 10f) * MathF.Exp(-2.2f * t);
                buffer[offset + i] = (saw + noise) * env * 0.3f;
            }
            return read;
        }
    }

    // --- Looping voices ---

    abstract class LoopingVoice : ISampleProvider
    {
        protected readonly int _sampleRate;
        protected int _sample;
        bool _stopping;
        int _stopSample;
        protected const int FadeInSamples  = 4410;
        protected const int FadeOutSamples = 4410;
        public WaveFormat WaveFormat { get; }

        protected LoopingVoice(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        }

        public void Stop()
        {
            if (_stopping) return;
            _stopping = true;
            _stopSample = _sample;
        }

        protected float Envelope()
        {
            float env = 1f;
            if (_sample < FadeInSamples) env = (float)_sample / FadeInSamples;
            if (_stopping)
            {
                int since = _sample - _stopSample;
                if (since >= FadeOutSamples) return -1f;
                env *= 1f - (float)since / FadeOutSamples;
            }
            return env;
        }

        public abstract int Read(float[] buffer, int offset, int count);
    }

    sealed class ThrustLoop : LoopingVoice
    {
        readonly Random _rng = new();
        float _filter;
        public ThrustLoop(int sampleRate) : base(sampleRate) { }
        public override int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count; i++, _sample++, read++)
            {
                float env = Envelope();
                if (env < 0f) return read;
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _filter = _filter * 0.82f + noise * 0.18f;
                float bp = noise - _filter;
                buffer[offset + i] = bp * env * 0.32f;
            }
            return read;
        }
    }

    sealed class SaucerHum : LoopingVoice
    {
        readonly float _baseFreq;
        readonly float _overtoneFreq;
        const float ModFreq = 2.8f;

        public SaucerHum(int sampleRate, bool large) : base(sampleRate)
        {
            _baseFreq     = large ? 70f  : 140f;
            _overtoneFreq = large ? 140f : 280f;
        }

        public override int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count; i++, _sample++, read++)
            {
                float env = Envelope();
                if (env < 0f) return read;
                float t = (float)_sample / _sampleRate;
                float baseTone = MathF.Sin(2f * MathF.PI * _baseFreq * t);
                float over     = MathF.Sin(2f * MathF.PI * _overtoneFreq * t);
                float warble   = 0.55f + 0.45f * MathF.Sin(2f * MathF.PI * ModFreq * t);
                float sample   = (baseTone * 0.55f + over * 0.20f) * warble;
                buffer[offset + i] = sample * env * 0.38f;
            }
            return read;
        }
    }
#endif
}
