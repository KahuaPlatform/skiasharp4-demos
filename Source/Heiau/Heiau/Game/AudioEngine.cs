#if HAS_NAUDIO
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
#endif

namespace Heiau.Game;

// Procedural sound effects for Heiau (Star-Castle-style ring shooter).
// Desktop: NAudio synth voices. WASM: Web Audio via JS interop.
public static class AudioEngine
{
#if HAS_NAUDIO
    const int SampleRate = 44100;
    static WaveOutEvent? _output;
    static MixingSampleProvider? _mixer;
    static bool _initialized;
    static ThrustLoop? _thrust;
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
        _thrust = null;
#endif
    }

    public static void PlayShoot()
    {
#if HAS_NAUDIO
        TryPlay(new ShootSound(SampleRate));
#endif
        WasmPlay("globalThis.heiauAudio && globalThis.heiauAudio.playShoot();");
    }

    public static void PlayRingHit()
    {
#if HAS_NAUDIO
        TryPlay(new RingHitSound(SampleRate));
#endif
        WasmPlay("globalThis.heiauAudio && globalThis.heiauAudio.playRingHit();");
    }

    public static void PlayTurretFire()
    {
#if HAS_NAUDIO
        TryPlay(new TurretFireSound(SampleRate));
#endif
        WasmPlay("globalThis.heiauAudio && globalThis.heiauAudio.playTurretFire();");
    }

    public static void PlayTurretKill()
    {
#if HAS_NAUDIO
        TryPlay(new TurretKillSound(SampleRate));
#endif
        WasmPlay("globalThis.heiauAudio && globalThis.heiauAudio.playTurretKill();");
    }

    public static void PlayShipExplosion()
    {
#if HAS_NAUDIO
        TryPlay(new ExplosionSound(SampleRate));
#endif
        WasmPlay("globalThis.heiauAudio && globalThis.heiauAudio.playShipExplosion();");
    }

    public static void StartThrust()
    {
#if HAS_NAUDIO
        if (_thrust != null) return;
        _thrust = new ThrustLoop(SampleRate);
        TryPlay(_thrust);
#endif
        WasmPlay("globalThis.heiauAudio && globalThis.heiauAudio.startThrust();");
    }

    public static void StopThrust()
    {
#if HAS_NAUDIO
        _thrust?.Stop();
        _thrust = null;
#endif
        WasmPlay("globalThis.heiauAudio && globalThis.heiauAudio.stopThrust();");
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

    // Square-wave shot bleep, brief.
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
                float freq = 1000f - t * 700f;
                float phase = (_sample * freq / _sampleRate) % 1f;
                float square = phase < 0.5f ? 0.28f : -0.28f;
                float env = 1f - t * 0.6f;
                buffer[offset + i] = square * env;
            }
            return read;
        }
    }

    // Ring hit: short metallic ping — sine + harmonic with fast decay.
    sealed class RingHitSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        int _sample;
        public WaveFormat WaveFormat { get; }

        public RingHitSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.18 * sampleRate);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float ang1 = 2f * MathF.PI * 1320f * _sample / _sampleRate;
                float ang2 = 2f * MathF.PI * 1980f * _sample / _sampleRate;
                float s = MathF.Sin(ang1) * 0.6f + MathF.Sin(ang2) * 0.3f;
                float env = MathF.Exp(-9f * t);
                buffer[offset + i] = s * env * 0.25f;
            }
            return read;
        }
    }

    // Turret fire: deep tom-like hit — short sine with quick pitch drop.
    sealed class TurretFireSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        int _sample;
        public WaveFormat WaveFormat { get; }

        public TurretFireSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(0.18 * sampleRate);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float freq = 260f - t * 180f;
                float phase = (_sample * freq / _sampleRate) % 1f;
                float saw = phase * 2f - 1f;
                float env = MathF.Exp(-4.5f * t);
                buffer[offset + i] = saw * env * 0.35f;
            }
            return read;
        }
    }

    // Turret kill: descending swell + filtered noise tail. ~1 second.
    sealed class TurretKillSound : ISampleProvider
    {
        readonly int _sampleRate;
        readonly int _totalSamples;
        readonly Random _rng = new();
        int _sample;
        float _filter;
        public WaveFormat WaveFormat { get; }

        public TurretKillSound(int sampleRate)
        {
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples = (int)(1.0 * sampleRate);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;
            for (int i = 0; i < count && _sample < _totalSamples; i++, _sample++, read++)
            {
                float t = (float)_sample / _totalSamples;
                float freq = 900f * MathF.Pow(1f - t, 1.4f) + 80f;
                float phase = (_sample * freq / _sampleRate) % 1f;
                float saw = phase * 2f - 1f;
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _filter = _filter * 0.85f + noise * 0.15f;
                float env = MathF.Min(1f, t * 12f) * MathF.Exp(-2.0f * t);
                buffer[offset + i] = (saw * 0.55f + _filter * 0.35f) * env;
            }
            return read;
        }
    }

    // Filtered-noise ship explosion.
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
                _filter = _filter * 0.72f + noise * 0.28f;
                float env = MathF.Exp(-3.0f * t);
                buffer[offset + i] = _filter * env * 0.6f;
            }
            return read;
        }
    }

    // Looping rocket thrust.
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
                buffer[offset + i] = bp * env * 0.28f;
            }
            return read;
        }
    }
#endif
}
