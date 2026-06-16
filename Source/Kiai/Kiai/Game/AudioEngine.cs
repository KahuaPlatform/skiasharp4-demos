#if HAS_NAUDIO
using NAudio.Wave;
#endif
using System;

namespace Kiai.Game;

// Procedural sound effects for Kia'i (Defender-style patrol shooter). Plumbing
// lives in Arcade.Common.Audio.AudioEngineBase; this is the static facade plus
// the per-voice synths. Desktop uses NAudio sample providers; wasm mirrors each
// voice via globalThis.kiaiAudio in audio.js (JS interop, fire-and-forget).
//
// Voices (per DESIGN): one-shots PlayShoot / PlayExplosion / PlayHyperspace /
// PlaySmartBomb / PlayHumanoidRescued (rising chime) / PlayHumanoidLost / PlayMutate,
// and the looping StartThrust / StopThrust pair.
public static class AudioEngine
{
    static readonly AudioEngineImpl _impl = new();
    public static void Init()                 => _impl.Init();
    public static void Shutdown()             => _impl.Shutdown();
    public static void PlayShoot()            => _impl.PlayShoot();
    public static void PlayExplosion()        => _impl.PlayExplosion();
    public static void PlayHyperspace()       => _impl.PlayHyperspace();
    public static void PlaySmartBomb()        => _impl.PlaySmartBomb();
    public static void PlayHumanoidRescued()  => _impl.PlayHumanoidRescued();
    public static void PlayHumanoidLost()     => _impl.PlayHumanoidLost();
    public static void PlayMutate()           => _impl.PlayMutate();
    public static void StartThrust()          => _impl.StartThrust();
    public static void StopThrust()           => _impl.StopThrust();

    sealed class AudioEngineImpl : AudioEngineBase
    {
#if HAS_NAUDIO
        ThrustLoop? _thrust;
#endif

        public void PlayShoot()
        {
#if HAS_NAUDIO
            TryPlay(new ShootSound(SampleRate));
#endif
            WasmPlay("globalThis.kiaiAudio && globalThis.kiaiAudio.playShoot();");
        }
        public void PlayExplosion()
        {
#if HAS_NAUDIO
            TryPlay(new ExplosionSound(SampleRate));
#endif
            WasmPlay("globalThis.kiaiAudio && globalThis.kiaiAudio.playExplosion();");
        }
        public void PlayHyperspace()
        {
#if HAS_NAUDIO
            TryPlay(new HyperspaceSound(SampleRate));
#endif
            WasmPlay("globalThis.kiaiAudio && globalThis.kiaiAudio.playHyperspace();");
        }
        public void PlaySmartBomb()
        {
#if HAS_NAUDIO
            TryPlay(new SmartBombSound(SampleRate));
#endif
            WasmPlay("globalThis.kiaiAudio && globalThis.kiaiAudio.playSmartBomb();");
        }
        public void PlayHumanoidRescued()
        {
#if HAS_NAUDIO
            TryPlay(new ChimeSound(SampleRate, rising: true));
#endif
            WasmPlay("globalThis.kiaiAudio && globalThis.kiaiAudio.playHumanoidRescued();");
        }
        public void PlayHumanoidLost()
        {
#if HAS_NAUDIO
            TryPlay(new ChimeSound(SampleRate, rising: false));
#endif
            WasmPlay("globalThis.kiaiAudio && globalThis.kiaiAudio.playHumanoidLost();");
        }
        public void PlayMutate()
        {
#if HAS_NAUDIO
            TryPlay(new MutateSound(SampleRate));
#endif
            WasmPlay("globalThis.kiaiAudio && globalThis.kiaiAudio.playMutate();");
        }
        public void StartThrust()
        {
#if HAS_NAUDIO
            if (_thrust != null) return;
            _thrust = new ThrustLoop(SampleRate);
            TryPlay(_thrust);
#endif
            WasmPlay("globalThis.kiaiAudio && globalThis.kiaiAudio.startThrust();");
        }
        public void StopThrust()
        {
#if HAS_NAUDIO
            _thrust?.Stop();
            _thrust = null;
#endif
            WasmPlay("globalThis.kiaiAudio && globalThis.kiaiAudio.stopThrust();");
        }
    }

#if HAS_NAUDIO
    // --- One-shot voices ---

    // Short downward square chirp — the player's blaster.
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
                float freq = 1040f - t * 760f;
                float phase = (_sample * freq / _sampleRate) % 1f;
                float square = phase < 0.5f ? 0.30f : -0.30f;
                float env = 1f - t * 0.5f;
                buffer[offset + i] = square * env;
            }
            return read;
        }
    }

    // Filtered noise burst with exponential decay — generic explosion.
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

    // Descending saw + noise sweep — teleport whoosh.
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

    // Long bright noise wash + falling tone — the screen-clearing smart bomb.
    sealed class SmartBombSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        readonly Random _rng = new();
        int _sample;
        float _filter;
        public WaveFormat WaveFormat { get; }
        public SmartBombSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.7 * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _filter = _filter * 0.5f + noise * 0.5f;
                float freq = 520f * (1f - t) + 60f;
                float phase = (_sample * freq / _sampleRate) % 1f;
                float tone = phase < 0.5f ? 1f : -1f;
                float env = MathF.Exp(-2.0f * t);
                buffer[offset + i] = (_filter * 0.5f + tone * 0.3f) * env * 0.5f;
            }
            return read;
        }
    }

    // Short arpeggio — rising for a rescue (happy), falling for a loss.
    sealed class ChimeSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        readonly float[] _notes;
        int _sample;
        public WaveFormat WaveFormat { get; }
        public ChimeSound(int sampleRate, bool rising)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.36 * sampleRate);
            _notes = rising
                ? new[] { 523.25f, 659.25f, 783.99f }   // C5 E5 G5 — rescue
                : new[] { 659.25f, 523.25f, 392.00f };  // E5 C5 G4 — loss
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            int per = Math.Max(1, _totalSamples / _notes.Length);
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                int note = Math.Min(_notes.Length - 1, _sample / per);
                float localT = (float)(_sample - note * per) / per;
                float freq = _notes[note];
                float s = MathF.Sin(2f * MathF.PI * freq * _sample / _sampleRate);
                float env = MathF.Sin(MathF.PI * localT) * 0.35f;   // gentle per-note swell
                buffer[offset + i] = s * env;
            }
            return read;
        }
    }

    // Warbling rising tone — a Lander completing an abduction and mutating.
    sealed class MutateSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        int _sample;
        public WaveFormat WaveFormat { get; }
        public MutateSound(int sampleRate)
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
                float freq = 220f + 600f * t;
                float warble = 1f + 0.25f * MathF.Sin(2f * MathF.PI * 18f * t);
                float phase = (_sample * freq * warble / _sampleRate) % 1f;
                float saw = phase * 2f - 1f;
                float env = MathF.Sin(MathF.PI * t) * 0.4f;
                buffer[offset + i] = saw * env;
            }
            return read;
        }
    }

    // --- Looping voice: thrust ---

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

    // Bandpass-flavoured white noise — engine rumble while thrusting.
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
                if (env < 0f) return read;
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _filter = _filter * 0.82f + noise * 0.18f;
                float bp = noise - _filter;
                buffer[offset + i] = bp * env * 0.30f;
            }
            return read;
        }
    }
#endif
}
