#if HAS_NAUDIO
using NAudio.Wave;
#endif
using System;

namespace Eli.Game;

// Procedural sound effects for Eli (Dig Dug-style tunnelling). Plumbing lives in
// `Arcade.Common.Audio.AudioEngineBase`; this facade exposes the static surface the
// game code calls. WASM dispatches via `globalThis.eliAudio`
// (see Platforms/WebAssembly/WasmScripts/audio.js), voice for voice.
public static class AudioEngine
{
    // Throttle the high-frequency voices so a continuously-digging player or a
    // held pump doesn't fuse into a buzz — same idiom as Hahai's chomp throttle.
    static float _lastDig;
    static float _lastPump;

    static readonly AudioEngineImpl _impl = new();
    public static void Init()     => _impl.Init();
    public static void Shutdown() => _impl.Shutdown();

    static float Now => (Environment.TickCount & int.MaxValue) * 0.001f;

    // The scrape of cutting earth — fires continuously while carving, so it is
    // throttled hardest.
    public static void PlayDig()
    {
        if (Now - _lastDig < 0.09f) return;
        _lastDig = Now;
        _impl.PlayDig();
    }

    // Pitch rises with inflation (0..1), so the ear tracks how close the pop is.
    public static void PlayPump(float inflation01)
    {
        if (Now - _lastPump < 0.05f) return;
        _lastPump = Now;
        _impl.PlayPump(Math.Clamp(inflation01, 0f, 1f));
    }

    public static void PlayHarpoonFire()  => _impl.PlayHarpoonFire();
    public static void PlayHarpoonStick() => _impl.PlayHarpoonStick();
    public static void PlayBurst()        => _impl.PlayBurst();
    public static void PlayPhase()        => _impl.PlayPhase();
    public static void PlayRockWobble()   => _impl.PlayRockWobble();
    public static void PlayRockFall()     => _impl.PlayRockFall();
    public static void PlayRockShatter()  => _impl.PlayRockShatter();
    public static void PlayDeath()        => _impl.PlayDeath();
    public static void PlayLevelClear()   => _impl.PlayLevelClear();

    sealed class AudioEngineImpl : AudioEngineBase
    {
        public void PlayDig()
        {
#if HAS_NAUDIO
            TryPlay(new NoiseBurst(SampleRate, 0.07f, 0.13f, lowpass: 0.82f));
#endif
            WasmPlay("globalThis.eliAudio && globalThis.eliAudio.playDig();");
        }
        public void PlayHarpoonFire()
        {
#if HAS_NAUDIO
            TryPlay(new BlipSound(SampleRate, 300f, 900f, 0.09f, 0.17f));
#endif
            WasmPlay("globalThis.eliAudio && globalThis.eliAudio.playHarpoonFire();");
        }
        public void PlayHarpoonStick()
        {
#if HAS_NAUDIO
            TryPlay(new BlipSound(SampleRate, 900f, 420f, 0.05f, 0.15f));
#endif
            WasmPlay("globalThis.eliAudio && globalThis.eliAudio.playHarpoonStick();");
        }
        public void PlayPump(float t)
        {
            // Each pump steps the pitch up, so a nearly-burst monster sings high.
            float f0 = 340f + 260f * t;
#if HAS_NAUDIO
            TryPlay(new BlipSound(SampleRate, f0, f0 * 1.45f, 0.10f, 0.16f));
#endif
            WasmPlay($"globalThis.eliAudio && globalThis.eliAudio.playPump({f0.ToString(System.Globalization.CultureInfo.InvariantCulture)});");
        }
        public void PlayBurst()
        {
#if HAS_NAUDIO
            TryPlay(new NoiseBurst(SampleRate, 0.28f, 0.45f, lowpass: 0.5f));
#endif
            WasmPlay("globalThis.eliAudio && globalThis.eliAudio.playBurst();");
        }
        public void PlayPhase()
        {
#if HAS_NAUDIO
            TryPlay(new BlipSound(SampleRate, 620f, 210f, 0.30f, 0.10f));
#endif
            WasmPlay("globalThis.eliAudio && globalThis.eliAudio.playPhase();");
        }
        public void PlayRockWobble()
        {
#if HAS_NAUDIO
            TryPlay(new ArpeggioSound(SampleRate, new[] { 150f, 130f, 150f, 130f }, 0.55f));
#endif
            WasmPlay("globalThis.eliAudio && globalThis.eliAudio.playRockWobble();");
        }
        public void PlayRockFall()
        {
#if HAS_NAUDIO
            TryPlay(new NoiseBurst(SampleRate, 0.45f, 0.40f, lowpass: 0.88f));
#endif
            WasmPlay("globalThis.eliAudio && globalThis.eliAudio.playRockFall();");
        }
        public void PlayRockShatter()
        {
#if HAS_NAUDIO
            TryPlay(new NoiseBurst(SampleRate, 0.22f, 0.50f, lowpass: 0.3f));
#endif
            WasmPlay("globalThis.eliAudio && globalThis.eliAudio.playRockShatter();");
        }
        public void PlayDeath()
        {
#if HAS_NAUDIO
            TryPlay(new DeathSound(SampleRate));
#endif
            WasmPlay("globalThis.eliAudio && globalThis.eliAudio.playDeath();");
        }
        public void PlayLevelClear()
        {
#if HAS_NAUDIO
            TryPlay(new ArpeggioSound(SampleRate, new[] { 392f, 493.88f, 587.33f, 783.99f, 1046.50f }, 0.7f));
#endif
            WasmPlay("globalThis.eliAudio && globalThis.eliAudio.playLevelClear();");
        }
    }

#if HAS_NAUDIO
    // A short pitch-swept square blip: harpoon fire/stick, pump, phase.
    sealed class BlipSound : ISampleProvider
    {
        readonly int _sampleRate, _total;
        readonly float _f0, _f1, _amp;
        int _s;
        public WaveFormat WaveFormat { get; }
        public BlipSound(int sampleRate, float f0, float f1, float seconds, float amp)
        {
            _sampleRate = sampleRate; _f0 = f0; _f1 = f1; _amp = amp;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _total = (int)(seconds * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _s < _total; i++, _s++, read++)
            {
                float t = (float)_s / _total;
                float freq = _f0 + (_f1 - _f0) * t;
                float phase = (_s * freq / _sampleRate) % 1f;
                float sq = phase < 0.5f ? 1f : -1f;
                float env = MathF.Exp(-4f * t);
                buffer[offset + i] = sq * env * _amp;
            }
            return read;
        }
    }

    // Filtered noise: the dig scrape, the pop, the rock fall and its shatter. The
    // lowpass coefficient is what separates "gritty earth" from "sharp crack".
    sealed class NoiseBurst : ISampleProvider
    {
        readonly int _sampleRate, _total;
        readonly float _amp, _lowpass;
        readonly Random _rng = new();
        float _filter;
        int _s;
        public WaveFormat WaveFormat { get; }
        public NoiseBurst(int sampleRate, float seconds, float amp, float lowpass)
        {
            _sampleRate = sampleRate; _amp = amp; _lowpass = Math.Clamp(lowpass, 0f, 0.95f);
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _total = (int)(seconds * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _s < _total; i++, _s++, read++)
            {
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _filter = _filter * _lowpass + noise * (1f - _lowpass);
                float env = MathF.Exp(-3.5f * (float)_s / _total);
                buffer[offset + i] = _filter * env * _amp;
            }
            return read;
        }
    }

    // Triangle arpeggio: the rock-wobble warning and the level-clear fanfare.
    sealed class ArpeggioSound : ISampleProvider
    {
        readonly int _sampleRate, _total;
        readonly float[] _freqs;
        int _s;
        public WaveFormat WaveFormat { get; }
        public ArpeggioSound(int sampleRate, float[] freqs, float seconds)
        {
            _sampleRate = sampleRate; _freqs = freqs;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _total = (int)(seconds * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            int noteSamples = Math.Max(1, _total / _freqs.Length);
            for (int i = 0; i < count && _s < _total; i++, _s++, read++)
            {
                int noteIdx = Math.Min(_freqs.Length - 1, _s / noteSamples);
                float t = (float)(_s - noteIdx * noteSamples) / noteSamples;
                float phase = (_s * _freqs[noteIdx] / _sampleRate) % 1f;
                float tri = 4f * MathF.Abs(phase - 0.5f) - 1f;
                float env = MathF.Exp(-2.5f * t);
                buffer[offset + i] = tri * env * 0.22f;
            }
            return read;
        }
    }

    // Descending wobble — the digger's death.
    sealed class DeathSound : ISampleProvider
    {
        readonly int _sampleRate, _total;
        int _s;
        public WaveFormat WaveFormat { get; }
        public DeathSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _total = (int)(1.2 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _s < _total; i++, _s++, read++)
            {
                float t = (float)_s / _total;
                float freq = 700f - t * 560f;
                float wobble = 1f + 0.18f * MathF.Sin(MathF.PI * 2f * t * 12f);
                float phase = (_s * freq * wobble / _sampleRate) % 1f;
                float saw = phase * 2f - 1f;
                float env = MathF.Exp(-1.6f * t);
                buffer[offset + i] = saw * env * 0.22f;
            }
            return read;
        }
    }
#endif
}
