#if HAS_NAUDIO
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
#endif

namespace HokuLele.Game;

// Procedural sound effects for the game. On net10.0-desktop we synthesise via NAudio
// (no sample files needed — every effect is generated from simple oscillators + envelopes
// in code). On wasm/other TFMs all calls are no-ops; wasm audio would need Web Audio
// via JS interop, which is out of scope for the stage demo.
public static class AudioEngine
{
#if HAS_NAUDIO
    const int SampleRate = 44100;
    static WaveOutEvent? _output;
    static MixingSampleProvider? _mixer;
    static bool _initialized;
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
            // No audio device — fail silent.
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
#endif
    }

    public static void PlayShoot()
    {
#if HAS_NAUDIO
        TryPlay(new ShootSound(SampleRate));
#endif
        WasmPlay("globalThis.hokuLeleAudio && globalThis.hokuLeleAudio.playShoot();");
    }

    public static void PlayExplosion()
    {
#if HAS_NAUDIO
        TryPlay(new ExplosionSound(SampleRate));
#endif
        WasmPlay("globalThis.hokuLeleAudio && globalThis.hokuLeleAudio.playExplosion();");
    }

    public static void PlayDive()
    {
#if HAS_NAUDIO
        TryPlay(new DiveSound(SampleRate));
#endif
        WasmPlay("globalThis.hokuLeleAudio && globalThis.hokuLeleAudio.playDive();");
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

    // --- Procedural synth voices ---

    // Short square-wave bleep with a downward frequency sweep. Fires once per shot.
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
                float freq = 880f - t * 660f;  // 880 Hz -> 220 Hz
                float phase = (_sample * freq / _sampleRate) % 1f;
                float square = phase < 0.5f ? 0.32f : -0.32f;
                float env = 1f - t * 0.5f;
                buffer[offset + i] = square * env;
            }
            return read;
        }
    }

    // White noise through a one-pole lowpass with exponential decay — explosion.
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

    // Sawtooth descending sweep with a soft attack/decay — the classic dive whoosh.
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
                float freq = 500f - t * 400f;  // 500 -> 100 Hz
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
