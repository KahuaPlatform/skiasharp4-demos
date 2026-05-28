#if HAS_NAUDIO
using NAudio.Wave;
#endif

namespace Lua.Game;

// Procedural sound effects for Lua (Tempest-style well shooter).
// Plumbing lives in `Arcade.Common.Audio.AudioEngineBase` — static facade.
public static class AudioEngine
{
    static readonly AudioEngineImpl _impl = new();
    public static void Init()      => _impl.Init();
    public static void Shutdown()  => _impl.Shutdown();
    public static void PlayShoot()     => _impl.PlayShoot();
    public static void PlayExplosion() => _impl.PlayExplosion();
    public static void PlayFlip()      => _impl.PlayFlip();
    public static void PlayZapper()    => _impl.PlayZapper();
    public static void PlayWarp()      => _impl.PlayWarp();

    sealed class AudioEngineImpl : AudioEngineBase
    {
        public void PlayShoot()
        {
#if HAS_NAUDIO
            TryPlay(new ShootSound(SampleRate));
#endif
            WasmPlay("globalThis.luaAudio && globalThis.luaAudio.playShoot();");
        }
        public void PlayExplosion()
        {
#if HAS_NAUDIO
            TryPlay(new ExplosionSound(SampleRate));
#endif
            WasmPlay("globalThis.luaAudio && globalThis.luaAudio.playExplosion();");
        }
        public void PlayFlip()
        {
#if HAS_NAUDIO
            TryPlay(new FlipSound(SampleRate));
#endif
            WasmPlay("globalThis.luaAudio && globalThis.luaAudio.playFlip();");
        }
        public void PlayZapper()
        {
#if HAS_NAUDIO
            TryPlay(new ZapperSound(SampleRate));
#endif
            WasmPlay("globalThis.luaAudio && globalThis.luaAudio.playZapper();");
        }
        public void PlayWarp()
        {
#if HAS_NAUDIO
            TryPlay(new WarpSound(SampleRate));
#endif
            WasmPlay("globalThis.luaAudio && globalThis.luaAudio.playWarp();");
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
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                float a = 0.95f - t * 0.5f;
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
