using System.Diagnostics;

namespace UnoGallery.Diagnostics;

/// <summary>
/// Lightweight per-frame profiler. Wrap any block in
/// <c>using (FrameProfiler.Measure("name")) { ... }</c> and the elapsed
/// milliseconds accumulate against that label for the current frame.
/// Call <see cref="EndFrame"/> once per frame to roll current totals into
/// the exponential moving average displayed in the on-canvas HUD.
///
/// Single-threaded by intent (UI thread only) — Skia rendering runs there.
/// If you need to time background-thread work, push the result back via a
/// separate channel.
/// </summary>
public static class FrameProfiler
{
    static readonly Dictionary<string, double> _current = new(32);
    static readonly Dictionary<string, double> _smoothed = new(32);
    static readonly List<string> _orderInsertion = new(32);
    const double EmaAlpha = 0.15;

    public static IDisposable Measure(string name) => new Scope(name);

    public static void Accumulate(string name, double ms)
    {
        if (!_current.ContainsKey(name)) _orderInsertion.Add(name);
        _current[name] = _current.GetValueOrDefault(name) + ms;
    }

    public static void EndFrame()
    {
        foreach (var name in _orderInsertion)
        {
            double cur = _current[name];
            double prev = _smoothed.GetValueOrDefault(name, cur);
            _smoothed[name] = prev * (1 - EmaAlpha) + cur * EmaAlpha;
            _current[name] = 0;
        }
    }

    /// <summary>Returns labels in first-seen order with smoothed ms values.</summary>
    public static IEnumerable<(string Label, double Ms)> Snapshot()
    {
        foreach (var name in _orderInsertion)
            if (_smoothed.TryGetValue(name, out var v))
                yield return (name, v);
    }

    sealed class Scope : IDisposable
    {
        readonly string _name;
        readonly long _start;
        public Scope(string name) { _name = name; _start = Stopwatch.GetTimestamp(); }
        public void Dispose()
        {
            var elapsed = Stopwatch.GetElapsedTime(_start);
            Accumulate(_name, elapsed.TotalMilliseconds);
        }
    }
}
