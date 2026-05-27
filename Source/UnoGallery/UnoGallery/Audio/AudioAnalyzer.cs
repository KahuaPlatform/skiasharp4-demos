using System.Numerics;

namespace UnoGallery.Audio;

/// <summary>
/// Per-frame audio analysis:
/// 1. Pull the most recent 1024 samples from the active source.
/// 2. Hann-window them and run an FFT.
/// 3. Smooth the magnitudes (exponential moving average) for steady display bars.
/// 4. Detect bass-band beats by comparing instantaneous bass energy against
///    a running mean+stddev with a 250 ms refractory period.
/// 5. Track a unit-range <see cref="Pulse"/> that snaps to 1 on a beat and
///    decays exponentially otherwise — drives ambient-background pulsing.
///
/// All state lives in this analyzer; pull data via the read-only spans
/// exposed for tile and pass consumers.
/// </summary>
public sealed class AudioAnalyzer
{
    public const int FftSize = 1024;
    public const int BinCount = FftSize / 2;

    readonly float[] _samples = new float[FftSize];
    readonly Complex[] _fftBuf = new Complex[FftSize];
    readonly float[] _smoothMag = new float[BinCount];
    readonly float[] _hann;

    // Beat-tracking state — exponential running mean/variance over bass energy.
    float _bassMean;
    float _bassVar;
    float _lastBeatTime = -1f;
    float _pulse;
    float _lastUpdateTime = -1f;
    bool _beatThisFrame;

    public ReadOnlySpan<float> Magnitudes => _smoothMag;
    public float Pulse => _pulse;
    public bool BeatJustDetected => _beatThisFrame;

    public AudioAnalyzer()
    {
        _hann = new float[FftSize];
        for (int i = 0; i < FftSize; i++)
            _hann[i] = 0.5f - 0.5f * MathF.Cos(2f * MathF.PI * i / (FftSize - 1));
    }

    public void Update(IAudioSource source, float wallClockSeconds)
    {
        _beatThisFrame = false;

        source.CopyLatest(_samples);

        for (int i = 0; i < FftSize; i++)
            _fftBuf[i] = new Complex(_samples[i] * _hann[i], 0.0);

        FFT.Forward(_fftBuf);

        // Magnitudes, with exponential smoothing for visual stability.
        const float SmoothAlpha = 0.35f;
        for (int i = 0; i < BinCount; i++)
        {
            var c = _fftBuf[i];
            float mag = (float)Math.Sqrt(c.Real * c.Real + c.Imaginary * c.Imaginary);
            _smoothMag[i] = _smoothMag[i] * (1f - SmoothAlpha) + mag * SmoothAlpha;
        }

        // Bass band ~ first six bins (bin 0 is DC). At 44.1 kHz / 1024-point FFT
        // each bin is ~43 Hz, so bins 1..5 cover ~43..215 Hz where kick drums live.
        float bassEnergy = 0f;
        for (int i = 1; i <= 5; i++)
        {
            var c = _fftBuf[i];
            bassEnergy += (float)(c.Real * c.Real + c.Imaginary * c.Imaginary);
        }

        // Update running mean/variance over a ~1 s window via EMA.
        const float StatAlpha = 0.06f;
        float diff = bassEnergy - _bassMean;
        _bassMean += diff * StatAlpha;
        _bassVar = _bassVar * (1f - StatAlpha) + diff * diff * StatAlpha;
        float stddev = MathF.Sqrt(_bassVar);
        float threshold = _bassMean + 2.0f * stddev;

        // Beat: instantaneous bass blows past the running threshold AND we're
        // past the refractory window AND the signal is above absolute silence.
        bool beat = bassEnergy > threshold
                    && bassEnergy > 0.5f
                    && wallClockSeconds - _lastBeatTime > 0.25f;

        if (beat)
        {
            _lastBeatTime = wallClockSeconds;
            _pulse = 1f;
            _beatThisFrame = true;
        }

        // Exponential decay of the pulse so the background swells out smoothly.
        if (_lastUpdateTime >= 0f)
        {
            float dt = MathF.Max(0f, wallClockSeconds - _lastUpdateTime);
            _pulse *= MathF.Exp(-dt * 3.5f);
        }
        _lastUpdateTime = wallClockSeconds;
    }
}
