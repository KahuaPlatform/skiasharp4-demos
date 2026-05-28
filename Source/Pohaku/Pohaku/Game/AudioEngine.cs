#if HAS_NAUDIO
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
#endif

namespace Pohaku.Game;

// Procedural sound effects for the game. On net10.0-desktop we synthesise via NAudio
// (no sample files needed). On net10.0-browserwasm we delegate to the procedural Web
// Audio voices in Platforms/WebAssembly/WasmScripts/audio.js via JS interop. Other
// TFMs are no-ops.
public static class AudioEngine
{
#if HAS_NAUDIO
    const int SampleRate = 44100;
    static WaveOutEvent? _output;
    static MixingSampleProvider? _mixer;
    static bool _initialized;

    // Stateful (looping) voices — we keep references so we can call Stop() on them.
    static ThrustLoop? _thrust;
    static SaucerHum? _saucer;
#endif

    public static void Init()
    {
#if HAS_NAUDIO
        if (_initialized) return;
        _initialized = true;
        try
        {
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1));
            _mixer.ReadFully = true;
            _output = new WaveOutEvent { DesiredLatency = 60 };
            _output.Init(_mixer);
            _output.Play();
        }
        catch
        {
            _output = null;
            _mixer = null;
        }
#endif
    }

    public static void Shutdown()
    {
#if HAS_NAUDIO
        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _mixer = null;
        _initialized = false;
        _thrust = null;
        _saucer = null;
#endif
    }

    // --- One-shot effects ---

    public static void PlayShoot()
    {
#if HAS_NAUDIO
        TryPlay(new ShootSound(SampleRate));
#endif
        WasmPlay("globalThis.pohakuAudio && globalThis.pohakuAudio.playShoot();");
    }

    public static void PlayExplosion()
    {
#if HAS_NAUDIO
        TryPlay(new ExplosionSound(SampleRate));
#endif
        WasmPlay("globalThis.pohakuAudio && globalThis.pohakuAudio.playExplosion();");
    }

    public static void PlayHyperspace()
    {
#if HAS_NAUDIO
        TryPlay(new HyperspaceSound(SampleRate));
#endif
        WasmPlay("globalThis.pohakuAudio && globalThis.pohakuAudio.playHyperspace();");
    }

    // --- Looping voices (call Start when entity becomes active, Stop when it disappears) ---

    public static void StartThrust()
    {
#if HAS_NAUDIO
        if (_thrust != null) return;
        _thrust = new ThrustLoop(SampleRate);
        TryPlay(_thrust);
#endif
        WasmPlay("globalThis.pohakuAudio && globalThis.pohakuAudio.startThrust();");
    }

    public static void StopThrust()
    {
#if HAS_NAUDIO
        _thrust?.Stop();
        _thrust = null;
#endif
        WasmPlay("globalThis.pohakuAudio && globalThis.pohakuAudio.stopThrust();");
    }

    public static void StartSaucer(bool large)
    {
#if HAS_NAUDIO
        if (_saucer != null) return;
        _saucer = new SaucerHum(SampleRate, large);
        TryPlay(_saucer);
#endif
        WasmPlay($"globalThis.pohakuAudio && globalThis.pohakuAudio.startSaucer({(large ? "true" : "false")});");
    }

    public static void StopSaucer()
    {
#if HAS_NAUDIO
        _saucer?.Stop();
        _saucer = null;
#endif
        WasmPlay("globalThis.pohakuAudio && globalThis.pohakuAudio.stopSaucer();");
    }

    static void WasmPlay(string js)
    {
#if __WASM__
        try { Uno.Foundation.WebAssemblyRuntime.InvokeJS(js); } catch { /* fail silent */ }
#endif
    }

#if HAS_NAUDIO
    static void TryPlay(ISampleProvider provider)
    {
        try { _mixer?.AddMixerInput(provider); } catch { }
    }

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

    // Hyperspace warp: exponential frequency sweep down from 1500Hz to 80Hz with a
    // chirpy sawtooth + brief noise overlay. ~320ms — quick enough to register as the
    // ship dematerialising and reappearing somewhere else.
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
                // Exponential descent: starts fast and slows out, so the "drop" feels weighty.
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

    // --- Looping voices: continue until Stop() is called, then fade out and signal removal ---

    abstract class LoopingVoice : ISampleProvider
    {
        protected readonly int _sampleRate;
        protected int _sample;
        bool _stopping;
        int _stopSample;
        protected const int FadeInSamples  = 4410;  // 100ms @ 44.1kHz
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
                if (since >= FadeOutSamples) return -1f;  // signal done
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
                if (env < 0f) return read;  // fade-out complete

                // Bandpass-flavoured white noise — rocket exhaust character
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
            // Large saucer ~70Hz fundamental; small saucer ~140Hz — matches the iconic
            // arcade convention where the small (deadlier) UFO has the higher pitch.
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
