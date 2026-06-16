#if HAS_NAUDIO
using NAudio.Wave;
#endif
using System;

namespace Koa.Game;

// Procedural sound effects for Koa (Gauntlet-style dungeon crawl). Plumbing lives
// in `Arcade.Common.Audio.AudioEngineBase`; this facade exposes the static
// surface the game code calls. WASM dispatches via `globalThis.koaAudio`
// (see Platforms/WebAssembly/WasmScripts/audio.js).
public static class AudioEngine
{
    // Throttle the high-frequency voices (shoot/hit) so a held trigger or a swarm
    // taking fire doesn't fuse into a continuous buzz — same idiom as Hahai's
    // chomp throttle.
    static float _lastShoot;
    static float _lastHit;

    static readonly AudioEngineImpl _impl = new();
    public static void Init()     => _impl.Init();
    public static void Shutdown() => _impl.Shutdown();

    static float Now => (Environment.TickCount & int.MaxValue) * 0.001f;

    public static void PlayShoot()
    {
        if (Now - _lastShoot < 0.06f) return;
        _lastShoot = Now;
        _impl.PlayShoot();
    }
    public static void PlayHit()
    {
        if (Now - _lastHit < 0.05f) return;
        _lastHit = Now;
        _impl.PlayHit();
    }
    public static void PlayEnemyDie()     => _impl.PlayEnemyDie();
    public static void PlayGeneratorDie()  => _impl.PlayGeneratorDie();
    public static void PlayPickup()        => _impl.PlayPickup();
    public static void PlayDoor()          => _impl.PlayDoor();
    public static void PlayPotion()        => _impl.PlayPotion();
    public static void PlayHeroHurt()      => _impl.PlayHeroHurt();
    public static void PlayDeath()         => _impl.PlayDeath();
    public static void PlayLevelClear()    => _impl.PlayLevelClear();
    public static void PlayLowHealth()     => _impl.PlayLowHealth();

    sealed class AudioEngineImpl : AudioEngineBase
    {
        public void PlayShoot()
        {
#if HAS_NAUDIO
            TryPlay(new BlipSound(SampleRate, 760f, 300f, 0.07f, 0.16f));
#endif
            WasmPlay("globalThis.koaAudio && globalThis.koaAudio.playShoot();");
        }
        public void PlayHit()
        {
#if HAS_NAUDIO
            TryPlay(new BlipSound(SampleRate, 380f, 200f, 0.05f, 0.12f));
#endif
            WasmPlay("globalThis.koaAudio && globalThis.koaAudio.playHit();");
        }
        public void PlayEnemyDie()
        {
#if HAS_NAUDIO
            TryPlay(new NoiseBurst(SampleRate, 0.22f, 0.45f));
#endif
            WasmPlay("globalThis.koaAudio && globalThis.koaAudio.playEnemyDie();");
        }
        public void PlayGeneratorDie()
        {
#if HAS_NAUDIO
            TryPlay(new NoiseBurst(SampleRate, 0.5f, 0.7f));
#endif
            WasmPlay("globalThis.koaAudio && globalThis.koaAudio.playGeneratorDie();");
        }
        public void PlayPickup()
        {
#if HAS_NAUDIO
            TryPlay(new ArpeggioSound(SampleRate, new[] { 523.25f, 659.25f, 783.99f }, 0.22f));
#endif
            WasmPlay("globalThis.koaAudio && globalThis.koaAudio.playPickup();");
        }
        public void PlayDoor()
        {
#if HAS_NAUDIO
            TryPlay(new BlipSound(SampleRate, 200f, 320f, 0.25f, 0.2f));
#endif
            WasmPlay("globalThis.koaAudio && globalThis.koaAudio.playDoor();");
        }
        public void PlayPotion()
        {
#if HAS_NAUDIO
            TryPlay(new ArpeggioSound(SampleRate, new[] { 392f, 523.25f, 659.25f, 880f }, 0.4f));
#endif
            WasmPlay("globalThis.koaAudio && globalThis.koaAudio.playPotion();");
        }
        public void PlayHeroHurt()
        {
#if HAS_NAUDIO
            TryPlay(new BlipSound(SampleRate, 300f, 120f, 0.12f, 0.2f));
#endif
            WasmPlay("globalThis.koaAudio && globalThis.koaAudio.playHeroHurt();");
        }
        public void PlayDeath()
        {
#if HAS_NAUDIO
            TryPlay(new DeathSound(SampleRate));
#endif
            WasmPlay("globalThis.koaAudio && globalThis.koaAudio.playDeath();");
        }
        public void PlayLevelClear()
        {
#if HAS_NAUDIO
            TryPlay(new ArpeggioSound(SampleRate, new[] { 392f, 493.88f, 587.33f, 783.99f, 1046.50f }, 0.7f));
#endif
            WasmPlay("globalThis.koaAudio && globalThis.koaAudio.playLevelClear();");
        }
        public void PlayLowHealth()
        {
            // The signature Gauntlet "warrior needs food badly" alert — a low
            // two-tone warning.
#if HAS_NAUDIO
            TryPlay(new ArpeggioSound(SampleRate, new[] { 330f, 247f }, 0.45f));
#endif
            WasmPlay("globalThis.koaAudio && globalThis.koaAudio.playLowHealth();");
        }
    }

#if HAS_NAUDIO
    // A short pitch-swept square blip used for shoot/hit/hurt/door. start->end
    // frequency over `seconds`, peak amplitude `amp`.
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

    // Filtered noise burst for enemy/generator destruction.
    sealed class NoiseBurst : ISampleProvider
    {
        readonly int _sampleRate, _total;
        readonly float _amp;
        readonly Random _rng = new();
        float _filter;
        int _s;
        public WaveFormat WaveFormat { get; }
        public NoiseBurst(int sampleRate, float seconds, float amp)
        {
            _sampleRate = sampleRate; _amp = amp;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _total = (int)(seconds * sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _s < _total; i++, _s++, read++)
            {
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _filter = _filter * 0.6f + noise * 0.4f;
                float env = MathF.Exp(-3.5f * (float)_s / _total);
                buffer[offset + i] = _filter * env * _amp;
            }
            return read;
        }
    }

    // Ascending/descending arpeggio for pickups/potion/level-clear/low-health.
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

    // Descending wobble — hero death.
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
