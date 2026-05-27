#if HAS_NAUDIO
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
#endif

namespace Lua.Game;

// Procedural sound effects for Lua (Tempest-style well shooter).
// Desktop: NAudio synth voices generated in code, no sample files.
// WASM: thin Uno.Foundation.WebAssemblyRuntime.InvokeJS shim that delegates to
// the procedural Web Audio voices in Platforms/WebAssembly/WasmScripts/audio.js.
// Other TFMs: no-ops.
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
        WasmPlay("globalThis.luaAudio && globalThis.luaAudio.playShoot();");
    }

    public static void PlayExplosion()
    {
#if HAS_NAUDIO
        TryPlay(new ExplosionSound(SampleRate));
#endif
        WasmPlay("globalThis.luaAudio && globalThis.luaAudio.playExplosion();");
    }

    // Short click whenever a Flipper flips between segments.
    public static void PlayFlip()
    {
#if HAS_NAUDIO
        TryPlay(new FlipSound(SampleRate));
#endif
        WasmPlay("globalThis.luaAudio && globalThis.luaAudio.playFlip();");
    }

    // Long descending sweep — Super Zapper, screen-clear.
    public static void PlayZapper()
    {
#if HAS_NAUDIO
        TryPlay(new ZapperSound(SampleRate));
#endif
        WasmPlay("globalThis.luaAudio && globalThis.luaAudio.playZapper();");
    }

    // Whoosh + ascending sweep — level transition, camera zooms down the well.
    public static void PlayWarp()
    {
#if HAS_NAUDIO
        TryPlay(new WarpSound(SampleRate));
#endif
        WasmPlay("globalThis.luaAudio && globalThis.luaAudio.playWarp();");
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

    // --- Procedural voices ---

    // Short square-wave bleep with descending pitch — player shot.
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
            _totalSamples = (int)(0.07 * sampleRate);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float freq = 1100f - t * 800f;
                float phase = (_sample * freq / _sampleRate) % 1f;
                float square = phase < 0.5f ? 0.28f : -0.28f;
                float env = 1f - t * 0.6f;
                buffer[offset + i] = square * env;
            }
            return read;
        }
    }

    // Filtered noise burst with exponential decay — enemy explosion.
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
            _totalSamples = (int)(0.30 * sampleRate);
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
                buffer[offset + i] = _filter * env * 0.5f;
            }
            return read;
        }
    }

    // Very short two-tone click — Flipper flipping between segments.
    sealed class FlipSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        int _sample;
        public WaveFormat WaveFormat { get; }

        public FlipSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.04 * sampleRate);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float freq = 320f + (t < 0.5f ? 0f : 240f);
                float phase = (_sample * freq / _sampleRate) % 1f;
                float square = phase < 0.5f ? 0.18f : -0.18f;
                float env = 1f - t;
                buffer[offset + i] = square * env;
            }
            return read;
        }
    }

    // Long descending modulated sweep — Super Zapper.
    sealed class ZapperSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        int _sample;
        public WaveFormat WaveFormat { get; }

        public ZapperSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.75 * sampleRate);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float modFreq = 28f;
                float modPhase = (_sample * modFreq / _sampleRate) % 1f;
                float modulator = (modPhase < 0.5f ? 1f : -1f);
                float baseFreq = 1400f * (1f - t * 0.85f);
                float freq = baseFreq + 240f * modulator;
                float phase = (_sample * freq / _sampleRate) % 1f;
                float saw = phase * 2f - 1f;
                float env = MathF.Min(1f, t * 6f) * (1f - t);
                buffer[offset + i] = saw * env * 0.30f;
            }
            return read;
        }
    }

    // Ascending whoosh — level transition warp.
    sealed class WarpSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        readonly Random _rng = new();
        int _sample;
        float _filter;
        public WaveFormat WaveFormat { get; }

        public WarpSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(2.0 * sampleRate);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                // Bandpass-ish filtered noise rising in pitch.
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                float a = 0.95f - t * 0.5f; // filter coefficient
                _filter = _filter * a + noise * (1f - a);
                float toneFreq = 120f + t * 1400f;
                float tonePhase = (_sample * toneFreq / _sampleRate) % 1f;
                float tone = (float)Math.Sin(tonePhase * Math.PI * 2.0);
                float env = MathF.Min(1f, t * 4f) * MathF.Min(1f, (1f - t) * 4f);
                buffer[offset + i] = (tone * 0.18f + _filter * 0.18f) * env;
            }
            return read;
        }
    }
#endif
}
