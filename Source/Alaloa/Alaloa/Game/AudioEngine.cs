#if HAS_NAUDIO
using NAudio.Wave;
#endif

namespace Alaloa.Game;

// Procedural sound effects for Alaloa (Tron-Light-Cycles-style demo).
// Plumbing lives in `Arcade.Common.Audio.AudioEngineBase`; this facade exposes
// the static surface the game code calls.
public static class AudioEngine
{
    static readonly AudioEngineImpl _impl = new();
    public static void Init()            => _impl.Init();
    public static void Shutdown()        => _impl.Shutdown();
    public static void PlayTurn()        => _impl.PlayTurn();
    public static void PlayCrash()       => _impl.PlayCrash();
    public static void PlayRoundWin()    => _impl.PlayRoundWin();
    public static void PlayRoundLose()   => _impl.PlayRoundLose();

    sealed class AudioEngineImpl : AudioEngineBase
    {
        public void PlayTurn()
        {
#if HAS_NAUDIO
            TryPlay(new TurnSound(SampleRate));
#endif
            WasmPlay("globalThis.alaloaAudio && globalThis.alaloaAudio.playTurn();");
        }
        public void PlayCrash()
        {
#if HAS_NAUDIO
            TryPlay(new CrashSound(SampleRate));
#endif
            WasmPlay("globalThis.alaloaAudio && globalThis.alaloaAudio.playCrash();");
        }
        public void PlayRoundWin()
        {
#if HAS_NAUDIO
            TryPlay(new RoundWinSound(SampleRate));
#endif
            WasmPlay("globalThis.alaloaAudio && globalThis.alaloaAudio.playRoundWin();");
        }
        public void PlayRoundLose()
        {
#if HAS_NAUDIO
            TryPlay(new RoundLoseSound(SampleRate));
#endif
            WasmPlay("globalThis.alaloaAudio && globalThis.alaloaAudio.playRoundLose();");
        }
    }

#if HAS_NAUDIO
    // Short high blip — every player turn.
    sealed class TurnSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        int _sample;
        public WaveFormat WaveFormat { get; }
        public TurnSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.03 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float ang = 2f * MathF.PI * 1400f * _sample / _sampleRate;
                float env = MathF.Exp(-15f * t);
                buffer[offset + i] = MathF.Sin(ang) * env * 0.18f;
            }
            return read;
        }
    }

    // Filtered-noise + dropping saw — cycle crash.
    sealed class CrashSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        readonly Random _rng = new();
        int _sample;
        float _filter;
        public WaveFormat WaveFormat { get; }
        public CrashSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.5 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _filter = _filter * 0.72f + noise * 0.28f;
                float freq = 320f - t * 240f;
                float phase = (_sample * freq / _sampleRate) % 1f;
                float saw = phase * 2f - 1f;
                float env = MathF.Exp(-3.0f * t);
                buffer[offset + i] = (_filter * 0.5f + saw * 0.4f) * env;
            }
            return read;
        }
    }

    // Rising arpeggio — round win fanfare.
    sealed class RoundWinSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        readonly float[] _freqs = { 523.25f, 659.25f, 783.99f, 1046.50f };
        int _sample;
        public WaveFormat WaveFormat { get; }
        public RoundWinSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.55 * sampleRate);
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

    // Descending tone — round lost.
    sealed class RoundLoseSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        readonly float[] _freqs = { 783.99f, 659.25f, 523.25f, 392.00f };
        int _sample;
        public WaveFormat WaveFormat { get; }
        public RoundLoseSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.55 * sampleRate);
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
#endif
}
