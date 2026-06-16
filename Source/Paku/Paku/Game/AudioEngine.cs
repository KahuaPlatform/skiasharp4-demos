#if HAS_NAUDIO
using NAudio.Wave;
#endif

namespace Paku.Game;

/// <summary>
/// Procedural sound effects for Paku, exposed as a static facade. Cross-platform
/// plumbing (NAudio mixer on desktop, JS interop on WASM) lives in
/// <see cref="AudioEngineBase"/>; this type only defines Paku's voices and
/// forwards calls to a private <see cref="AudioEngineBase"/> instance.
/// </summary>
public static class AudioEngine
{
    static readonly AudioEngineImpl _impl = new();
    /// <summary>Starts the audio device (desktop). No-op elsewhere.</summary>
    public static void Init()            => _impl.Init();
    /// <summary>Stops and disposes the audio device.</summary>
    public static void Shutdown()        => _impl.Shutdown();
    /// <summary>Rising bubbly chirp played when a cell or spore is absorbed.</summary>
    public static void PlayAbsorb()      => _impl.PlayAbsorb();
    /// <summary>Descending warble played when the player is eaten.</summary>
    public static void PlayDeath()       => _impl.PlayDeath();
    /// <summary>Begins the looping bubbly thrust voice (idempotent).</summary>
    public static void StartThrust()     => _impl.StartThrust();
    /// <summary>Fades out and ends the thrust voice.</summary>
    public static void StopThrust()      => _impl.StopThrust();

    // Concrete engine: each public method plays the desktop NAudio voice (if
    // built with HAS_NAUDIO) and fires the matching Web Audio call on WASM.
    sealed class AudioEngineImpl : AudioEngineBase
    {
#if HAS_NAUDIO
        ThrustBubble? _thrust;
#endif

        public void PlayAbsorb()
        {
#if HAS_NAUDIO
            TryPlay(new AbsorbSound(SampleRate));
#endif
            WasmPlay("globalThis.pakuAudio && globalThis.pakuAudio.playAbsorb();");
        }
        public void PlayDeath()
        {
#if HAS_NAUDIO
            TryPlay(new DeathSound(SampleRate));
#endif
            WasmPlay("globalThis.pakuAudio && globalThis.pakuAudio.playDeath();");
        }
        public void StartThrust()
        {
#if HAS_NAUDIO
            if (_thrust != null) return;
            _thrust = new ThrustBubble(SampleRate);
            TryPlay(_thrust);
#endif
            WasmPlay("globalThis.pakuAudio && globalThis.pakuAudio.startThrust();");
        }
        public void StopThrust()
        {
#if HAS_NAUDIO
            _thrust?.Stop();
            _thrust = null;
#endif
            WasmPlay("globalThis.pakuAudio && globalThis.pakuAudio.stopThrust();");
        }
    }

#if HAS_NAUDIO
    // --- One-shot voices ---

    // Absorb: rising bubbly chirp
    sealed class AbsorbSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        int _sample;
        public WaveFormat WaveFormat { get; }
        public AbsorbSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.12 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float freq = 300f + t * 900f; // rising chirp 300→1200 Hz
                float phase = (_sample * freq / _sampleRate) % 1f;
                float sine = MathF.Sin(phase * MathF.PI * 2f);
                float env = MathF.Sin(t * MathF.PI); // bell envelope
                buffer[offset + i] = sine * env * 0.28f;
            }
            return read;
        }
    }

    // Death: descending warble with noise
    sealed class DeathSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        readonly Random _rng = new();
        int _sample;
        float _filter;
        public WaveFormat WaveFormat { get; }
        public DeathSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.6 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float freq = 600f * MathF.Pow(1f - t, 1.5f) + 60f;
                float phase = (_sample * freq / _sampleRate) % 1f;
                float saw = phase * 2f - 1f;
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _filter = _filter * 0.7f + noise * 0.3f;
                float env = MathF.Exp(-2f * t);
                buffer[offset + i] = (saw * 0.4f + _filter * 0.3f) * env * 0.4f;
            }
            return read;
        }
    }

    // --- Looping voices ---

    // Base for indefinitely-sustained voices. Applies a linear fade-in at the
    // start and, once Stop() is requested, a linear fade-out — Envelope() returns
    // -1 once the fade-out completes so the voice can remove itself from the mixer.
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

    // Thrust: bubbly low-frequency filtered noise
    sealed class ThrustBubble : LoopingVoice
    {
        readonly Random _rng = new();
        float _filter1, _filter2;
        public ThrustBubble(int sampleRate) : base(sampleRate) { }
        public override int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count; i++, _sample++, read++)
            {
                float env = Envelope();
                if (env < 0f) return read;
                float t = (float)_sample / _sampleRate;
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                // Double-filtered for bubbly texture
                _filter1 = _filter1 * 0.88f + noise * 0.12f;
                _filter2 = _filter2 * 0.92f + _filter1 * 0.08f;
                // Add a slow wobble
                float wobble = MathF.Sin(t * MathF.PI * 2f * 5f) * 0.3f;
                buffer[offset + i] = (_filter2 + wobble * _filter1) * env * 0.25f;
            }
            return read;
        }
    }
#endif
}
