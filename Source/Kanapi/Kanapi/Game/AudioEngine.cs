#if HAS_NAUDIO
using NAudio.Wave;
#endif

namespace Kanapi.Game;

// Procedural sound effects for Kanapi (Centipede-style demo).
// Plumbing lives in `Arcade.Common.Audio.AudioEngineBase` — static facade.
public static class AudioEngine
{
    static readonly AudioEngineImpl _impl = new();
    public static void Init()              => _impl.Init();
    public static void Shutdown()          => _impl.Shutdown();
    public static void PlayShoot()         => _impl.PlayShoot();
    public static void PlayMushroomHit()   => _impl.PlayMushroomHit();
    public static void PlaySegmentKill()   => _impl.PlaySegmentKill();
    public static void PlaySpiderKill()    => _impl.PlaySpiderKill();
    public static void PlayPlayerDeath()   => _impl.PlayPlayerDeath();

    sealed class AudioEngineImpl : AudioEngineBase
    {
        public void PlayShoot()
        {
#if HAS_NAUDIO
            TryPlay(new ShootSound(SampleRate));
#endif
            WasmPlay("globalThis.kanapiAudio && globalThis.kanapiAudio.playShoot();");
        }
        public void PlayMushroomHit()
        {
#if HAS_NAUDIO
            TryPlay(new MushroomHitSound(SampleRate));
#endif
            WasmPlay("globalThis.kanapiAudio && globalThis.kanapiAudio.playMushroomHit();");
        }
        public void PlaySegmentKill()
        {
#if HAS_NAUDIO
            TryPlay(new SegmentKillSound(SampleRate));
#endif
            WasmPlay("globalThis.kanapiAudio && globalThis.kanapiAudio.playSegmentKill();");
        }
        public void PlaySpiderKill()
        {
#if HAS_NAUDIO
            TryPlay(new SpiderKillSound(SampleRate));
#endif
            WasmPlay("globalThis.kanapiAudio && globalThis.kanapiAudio.playSpiderKill();");
        }
        public void PlayPlayerDeath()
        {
#if HAS_NAUDIO
            TryPlay(new PlayerDeathSound(SampleRate));
#endif
            WasmPlay("globalThis.kanapiAudio && globalThis.kanapiAudio.playPlayerDeath();");
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
            _totalSamples = (int)(0.06 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float freq = 1300f - t * 800f;
                float phase = (_sample * freq / _sampleRate) % 1f;
                float square = phase < 0.5f ? 0.22f : -0.22f;
                float env = 1f - t * 0.7f;
                buffer[offset + i] = square * env;
            }
            return read;
        }
    }

    sealed class MushroomHitSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        int _sample;
        public WaveFormat WaveFormat { get; }
        public MushroomHitSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.06 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float ang = 2f * MathF.PI * 220f * _sample / _sampleRate;
                float env = MathF.Exp(-12f * t);
                buffer[offset + i] = MathF.Sin(ang) * env * 0.28f;
            }
            return read;
        }
    }

    sealed class SegmentKillSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        readonly Random _rng = new();
        int _sample;
        float _filter;
        public WaveFormat WaveFormat { get; }
        public SegmentKillSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.16 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _filter = _filter * 0.7f + noise * 0.3f;
                float ang = 2f * MathF.PI * 180f * _sample / _sampleRate;
                float tone = MathF.Sin(ang);
                float env = MathF.Exp(-7f * t);
                buffer[offset + i] = (_filter * 0.5f + tone * 0.4f) * env;
            }
            return read;
        }
    }

    sealed class SpiderKillSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        int _sample;
        public WaveFormat WaveFormat { get; }
        public SpiderKillSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.22 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float freq = 880f - t * 640f;
                float phase = (_sample * freq / _sampleRate) % 1f;
                float saw = phase * 2f - 1f;
                float env = MathF.Exp(-5f * t);
                buffer[offset + i] = saw * env * 0.30f;
            }
            return read;
        }
    }

    sealed class PlayerDeathSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        readonly Random _rng = new();
        int _sample;
        float _filter;
        public WaveFormat WaveFormat { get; }
        public PlayerDeathSound(int sampleRate)
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
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _filter = _filter * 0.8f + noise * 0.2f;
                float freq = 220f - t * 160f;
                float phase = (_sample * freq / _sampleRate) % 1f;
                float saw = phase * 2f - 1f;
                float env = MathF.Exp(-2.5f * t);
                buffer[offset + i] = (_filter * 0.5f + saw * 0.3f) * env;
            }
            return read;
        }
    }
#endif
}
