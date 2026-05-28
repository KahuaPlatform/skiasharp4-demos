#if HAS_NAUDIO
using NAudio.Wave;
#endif

namespace Mahina.Game;

// Procedural sound effects for Mahina (Lunar Lander).
// Plumbing lives in `Arcade.Common.Audio.AudioEngineBase` — this class is a
// static facade around a singleton instance for ergonomic call-site syntax.
public static class AudioEngine
{
    static readonly AudioEngineImpl _impl = new();
    public static void Init()              => _impl.Init();
    public static void Shutdown()          => _impl.Shutdown();
    public static void PlayExplosion()     => _impl.PlayExplosion();
    public static void PlayLandingChime()  => _impl.PlayLandingChime();
    public static void StartThrust()       => _impl.StartThrust();
    public static void StopThrust()        => _impl.StopThrust();

    sealed class AudioEngineImpl : AudioEngineBase
    {
#if HAS_NAUDIO
        ThrustLoop? _thrust;
#endif
        public void PlayExplosion()
        {
#if HAS_NAUDIO
            TryPlay(new ExplosionSound(SampleRate));
#endif
            WasmPlay("globalThis.mahinaAudio && globalThis.mahinaAudio.playExplosion();");
        }
        public void PlayLandingChime()
        {
#if HAS_NAUDIO
            TryPlay(new LandingChimeSound(SampleRate));
#endif
            WasmPlay("globalThis.mahinaAudio && globalThis.mahinaAudio.playLandingChime();");
        }
        public void StartThrust()
        {
#if HAS_NAUDIO
            if (_thrust != null) return;
            _thrust = new ThrustLoop(SampleRate);
            TryPlay(_thrust);
#endif
            WasmPlay("globalThis.mahinaAudio && globalThis.mahinaAudio.startThrust();");
        }
        public void StopThrust()
        {
#if HAS_NAUDIO
            _thrust?.Stop();
            _thrust = null;
#endif
            WasmPlay("globalThis.mahinaAudio && globalThis.mahinaAudio.stopThrust();");
        }
    }

#if HAS_NAUDIO
    // --- Procedural voices ---

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
            _totalSamples = (int)(0.45 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _filter = _filter * 0.72f + noise * 0.28f;
                float env = MathF.Exp(-2.8f * t);
                buffer[offset + i] = _filter * env * 0.6f;
            }
            return read;
        }
    }

    sealed class LandingChimeSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        readonly float[] _freqs = { 523.25f, 659.25f, 783.99f, 1046.50f };
        int _sample;
        public WaveFormat WaveFormat { get; }
        public LandingChimeSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.6 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            int noteSamples = _totalSamples / _freqs.Length;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                int noteIdx = Math.Min(_freqs.Length - 1, _sample / noteSamples);
                float t = (float)(_sample - noteIdx * noteSamples) / noteSamples;
                float phase = (_sample * _freqs[noteIdx] / _sampleRate) % 1f;
                float tri = 4f * MathF.Abs(phase - 0.5f) - 1f;
                float env = MathF.Exp(-3.0f * t);
                buffer[offset + i] = tri * env * 0.22f;
            }
            return read;
        }
    }

    sealed class ThrustLoop : ISampleProvider
    {
        readonly int _sampleRate;
        readonly Random _rng = new();
        int _sample;
        bool _stopping;
        int _stopSample;
        float _filter;
        const int FadeInSamples  = 2200;
        const int FadeOutSamples = 4400;
        public WaveFormat WaveFormat { get; }
        public ThrustLoop(int sampleRate)
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
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count; i++, _sample++, read++)
            {
                float env = 1f;
                if (_sample < FadeInSamples) env = (float)_sample / FadeInSamples;
                if (_stopping)
                {
                    int since = _sample - _stopSample;
                    if (since >= FadeOutSamples) return read;
                    env *= 1f - (float)since / FadeOutSamples;
                }
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _filter = _filter * 0.85f + noise * 0.15f;
                float bp = (noise - _filter) * 0.5f + _filter * 0.5f;
                float lfo = 0.85f + 0.15f * MathF.Sin(2f * MathF.PI * 4f * _sample / _sampleRate);
                buffer[offset + i] = bp * env * 0.35f * lfo;
            }
            return read;
        }
    }
#endif
}
