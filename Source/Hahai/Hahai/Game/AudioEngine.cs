#if HAS_NAUDIO
using NAudio.Wave;
#endif

namespace Hahai.Game;

// Procedural sound effects for Hahai (Pac-Man-style demo). Plumbing lives in
// `Arcade.Common.Audio.AudioEngineBase`; this facade exposes the static
// surface the game code calls. WASM dispatches via `globalThis.hahaiAudio`
// (see Platforms/WebAssembly/WasmScripts/audio.js).
public static class AudioEngine
{
    // Rate-limit per-pellet chomp — at 100px/sec eating chunky pellets every
    // frame would otherwise produce a continuous tone instead of the classic
    // "wakka wakka" alternation.
    static float _lastChompPlay;
    static bool  _chompFlip;

    static readonly AudioEngineImpl _impl = new();
    public static void Init()             => _impl.Init();
    public static void Shutdown()         => _impl.Shutdown();
    public static void PlayChomp()
    {
        // Throttle chomp to ~10 Hz alternating high/low for the bubble effect.
        float now = (System.Environment.TickCount & int.MaxValue) * 0.001f;
        if (now - _lastChompPlay < 0.10f) return;
        _lastChompPlay = now;
        _chompFlip = !_chompFlip;
        if (_chompFlip) _impl.PlayChompHi();
        else            _impl.PlayChompLo();
    }
    public static void PlayPower()        => _impl.PlayPower();
    public static void PlayEatGhost()     => _impl.PlayEatGhost();
    public static void PlayDeath()        => _impl.PlayDeath();
    public static void PlayLevelClear()   => _impl.PlayLevelClear();

    sealed class AudioEngineImpl : AudioEngineBase
    {
        public void PlayChompHi()
        {
#if HAS_NAUDIO
            TryPlay(new ChompSound(SampleRate, 900f));
#endif
            WasmPlay("globalThis.hahaiAudio && globalThis.hahaiAudio.playChomp(900);");
        }
        public void PlayChompLo()
        {
#if HAS_NAUDIO
            TryPlay(new ChompSound(SampleRate, 540f));
#endif
            WasmPlay("globalThis.hahaiAudio && globalThis.hahaiAudio.playChomp(540);");
        }
        public void PlayPower()
        {
#if HAS_NAUDIO
            TryPlay(new ArpeggioSound(SampleRate, new[] { 261.63f, 329.63f, 392.00f, 523.25f }, 0.55f));
#endif
            WasmPlay("globalThis.hahaiAudio && globalThis.hahaiAudio.playPower();");
        }
        public void PlayEatGhost()
        {
#if HAS_NAUDIO
            TryPlay(new ArpeggioSound(SampleRate, new[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.35f));
#endif
            WasmPlay("globalThis.hahaiAudio && globalThis.hahaiAudio.playEatGhost();");
        }
        public void PlayDeath()
        {
#if HAS_NAUDIO
            TryPlay(new DeathSound(SampleRate));
#endif
            WasmPlay("globalThis.hahaiAudio && globalThis.hahaiAudio.playDeath();");
        }
        public void PlayLevelClear()
        {
#if HAS_NAUDIO
            TryPlay(new ArpeggioSound(SampleRate, new[] { 392f, 493.88f, 587.33f, 783.99f, 1046.50f }, 0.7f));
#endif
            WasmPlay("globalThis.hahaiAudio && globalThis.hahaiAudio.playLevelClear();");
        }
    }

#if HAS_NAUDIO
    // Quick blip — pellet chomp. Two frequencies alternate via caller to give
    // the classic Pac alternation.
    sealed class ChompSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        readonly float _freq;
        int _sample;
        public WaveFormat WaveFormat { get; }
        public ChompSound(int sampleRate, float freq)
        {
            _sampleRate = sampleRate;
            _freq = freq;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.08 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float phase = (_sample * _freq / _sampleRate) % 1f;
                float tri = 4f * MathF.Abs(phase - 0.5f) - 1f;
                float env = MathF.Exp(-10f * t);
                buffer[offset + i] = tri * env * 0.18f;
            }
            return read;
        }
    }

    // Generic ascending or descending arpeggio used for power-up, eat-ghost,
    // and level-clear fanfares — pitches passed in, total duration too.
    sealed class ArpeggioSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly float[] _freqs;
        readonly int _totalSamples;
        int _sample;
        public WaveFormat WaveFormat { get; }
        public ArpeggioSound(int sampleRate, float[] freqs, float seconds)
        {
            _sampleRate = sampleRate;
            _freqs = freqs;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(seconds * sampleRate);
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
                float env = MathF.Exp(-2.5f * t);
                buffer[offset + i] = tri * env * 0.22f;
            }
            return read;
        }
    }

    // Descending pitch wobble — pac death.
    sealed class DeathSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        int _sample;
        public WaveFormat WaveFormat { get; }
        public DeathSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(1.2 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float freq = 900f - t * 700f;
                float wobble = 1f + 0.18f * MathF.Sin(MathF.PI * 2f * t * 14f);
                float phase = (_sample * freq * wobble / _sampleRate) % 1f;
                float saw = phase * 2f - 1f;
                float env = MathF.Exp(-1.5f * t);
                buffer[offset + i] = saw * env * 0.22f;
            }
            return read;
        }
    }
#endif
}
