#if HAS_NAUDIO
using System.Diagnostics;
using NAudio.Wave;

namespace UnoGallery.Audio;

/// <summary>
/// Real microphone capture via NAudio's WaveInEvent (Windows desktop only,
/// gated by the HAS_NAUDIO compile symbol set in the csproj for the
/// net10.0-desktop TFM). Samples land in <see cref="AudioRingBuffer"/>
/// from NAudio's thread-pool callback; the UI thread reads via
/// <see cref="CopyLatest"/>.
///
/// Captures mono PCM at 44.1 kHz so the buffer holds ~half a second of
/// audio in a 22050-sample ring — well past what the waveform tile
/// samples per frame.
/// </summary>
public sealed class NAudioMicrophoneSource : IAudioSource
{
    const int SampleRate = 44100;
    const int BufferSeconds = 1;

    readonly int _deviceNumber;
    readonly AudioRingBuffer _ring;
    readonly string _name;
    WaveInEvent? _waveIn;

    public string Name => _name;
    public bool IsRunning => _waveIn is not null;

    public NAudioMicrophoneSource(int deviceNumber, string name)
    {
        _deviceNumber = deviceNumber;
        _name = name;
        _ring = new AudioRingBuffer(SampleRate * BufferSeconds);
    }

    public static IReadOnlyList<AudioSourceInfo> EnumerateDevices()
    {
        var devices = new List<AudioSourceInfo>();
        try
        {
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                devices.Add(new AudioSourceInfo(
                    Id: $"mic:{i}",
                    DisplayName: $"Microphone: {caps.ProductName}"));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NAudio] device enumeration failed: {ex.Message}");
        }
        return devices;
    }

    public static int DeviceNumberFromId(string id)
    {
        // id is "mic:N"
        var span = id.AsSpan();
        int sep = span.IndexOf(':');
        if (sep < 0) return -1;
        return int.TryParse(span[(sep + 1)..], out var n) ? n : -1;
    }

    public void Start()
    {
        if (_waveIn is not null) return;
        try
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = _deviceNumber,
                WaveFormat = new WaveFormat(SampleRate, 16, 1),
                BufferMilliseconds = 30,
            };
            _waveIn.DataAvailable += OnData;
            _waveIn.RecordingStopped += OnStopped;
            _waveIn.StartRecording();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NAudio] StartRecording failed: {ex.Message}");
            _waveIn?.Dispose();
            _waveIn = null;
        }
    }

    public void Stop()
    {
        var w = _waveIn;
        _waveIn = null;
        if (w is null) return;
        try
        {
            w.StopRecording();
            w.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NAudio] Stop failed: {ex.Message}");
        }
    }

    public void Dispose() => Stop();

    public void CopyLatest(Span<float> dest) => _ring.CopyLatest(dest);

    void OnData(object? sender, WaveInEventArgs e)
    {
        // 16-bit PCM little-endian. Convert to float in-place into a stack buffer
        // then push to the ring. e.BytesRecorded may be < e.Buffer.Length.
        int sampleCount = e.BytesRecorded / 2;
        if (sampleCount <= 0) return;

        // Avoid stackalloc for large bursts; fall back to a heap array.
        Span<float> samples = sampleCount <= 1024 ? stackalloc float[sampleCount] : new float[sampleCount];
        var buf = e.Buffer;
        for (int i = 0; i < sampleCount; i++)
        {
            short s = (short)(buf[i * 2] | (buf[i * 2 + 1] << 8));
            samples[i] = s / 32768f;
        }
        _ring.Push(samples);
    }

    void OnStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
            Debug.WriteLine($"[NAudio] recording stopped with exception: {e.Exception.Message}");
    }
}
#endif
