namespace UnoGallery.Audio;

/// <summary>
/// Lock-protected single-producer / single-consumer ring buffer of floats.
/// The producer is whichever thread the audio callback runs on (NAudio
/// thread-pool, or the UI thread for the synth source); the consumer is
/// the UI thread reading from <see cref="CopyLatest"/> each frame.
/// </summary>
public sealed class AudioRingBuffer
{
    readonly float[] _buf;
    readonly Lock _lock = new();
    int _writeIndex;
    bool _filled;

    public AudioRingBuffer(int capacity)
    {
        if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
        _buf = new float[capacity];
    }

    public int Capacity => _buf.Length;

    public void Push(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0) return;
        lock (_lock)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                _buf[_writeIndex] = samples[i];
                _writeIndex = (_writeIndex + 1) % _buf.Length;
                if (_writeIndex == 0) _filled = true;
            }
        }
    }

    /// <summary>
    /// Fill <paramref name="dest"/> with the most recent samples — oldest at
    /// index 0, newest at <c>dest.Length - 1</c>. If the buffer hasn't yet
    /// captured enough samples, the front is zero-padded.
    /// </summary>
    public void CopyLatest(Span<float> dest)
    {
        int n = dest.Length;
        lock (_lock)
        {
            int available = _filled ? _buf.Length : _writeIndex;
            int take = Math.Min(n, available);
            int pad = n - take;
            for (int i = 0; i < pad; i++) dest[i] = 0f;

            int start = (_writeIndex - take + _buf.Length) % _buf.Length;
            for (int i = 0; i < take; i++)
                dest[pad + i] = _buf[(start + i) % _buf.Length];
        }
    }
}
