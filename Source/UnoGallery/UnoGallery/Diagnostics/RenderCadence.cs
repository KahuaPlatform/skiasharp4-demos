using System.Diagnostics;
using System.Text;

namespace UnoGallery.Diagnostics;

/// <summary>
/// Measures the render loop's <i>cadence</i>, which <see cref="FrameProfiler"/>
/// deliberately does not: FrameProfiler only times work that happens inside a
/// paint, so a loop that paints one perfect 8 ms frame per second looks healthy
/// to it. This class times the gaps instead.
///
/// Four independent signals, reported together once a second:
///
///   - <b>tick</b> — interval between <c>CompositionTarget.Rendering</c>
///     callbacks. This is the animation clock. If it is ~16 ms the frame source
///     is healthy; if it is ~1000 ms the UI thread or Uno's tick source is the
///     problem and nothing downstream matters.
///   - <b>paint</b> — interval between <c>RenderOverride</c> calls. A healthy
///     tick with a slow paint means <c>Invalidate()</c> requests are being
///     coalesced or dropped by the compositor.
///   - <b>tickCost</b> / <b>paintCost</b> — wall time actually spent inside our
///     own tick and paint code. If these are small while the intervals are
///     large, the time is being spent outside managed code (present, vsync
///     wait, GPU stall, dispatcher starvation).
///   - <b>latency</b> — tick → next paint delay, which isolates invalidation
///     plumbing from paint cost.
///
/// Output goes to stdout and to a log file (<c>UNOGALLERY_CADENCE_LOG</c>, or
/// <c>%TEMP%\unogallery-cadence.log</c>) because a Win32 desktop head may not
/// have an attached console. Set <c>UNOGALLERY_CADENCE=0</c> to disable.
/// </summary>
public static class RenderCadence
{
    public static bool Enabled { get; set; } =
        Environment.GetEnvironmentVariable("UNOGALLERY_CADENCE") != "0";

    const double ReportIntervalSeconds = 1.0;

    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static readonly Sampler TickInterval = new();
    static readonly Sampler PaintInterval = new();
    static readonly Sampler TickCost = new();
    static readonly Sampler PaintCost = new();
    static readonly Sampler TickToPaintLatency = new();

    static long _lastTickTs;
    static long _lastPaintTs;
    static long _pendingInvalidateTs;
    static int _paintsSinceTick;
    static int _ticksWithoutPaint;
    static double _lastReportAt;
    static StreamWriter? _file;
    static bool _fileTried;

    /// <summary>Call at the top of the frame-source callback.</summary>
    public static void BeginTick()
    {
        if (!Enabled) return;
        long now = Stopwatch.GetTimestamp();
        if (_lastTickTs != 0)
            TickInterval.Add(Stopwatch.GetElapsedTime(_lastTickTs, now).TotalMilliseconds);
        _lastTickTs = now;

        // A tick that produced no paint since the previous tick means the
        // invalidate never turned into a RenderOverride.
        if (_paintsSinceTick == 0) _ticksWithoutPaint++;
        _paintsSinceTick = 0;
    }

    /// <summary>Call after the tick body (state update + Invalidate) completes.</summary>
    public static void EndTick()
    {
        if (!Enabled) return;
        TickCost.Add(Stopwatch.GetElapsedTime(_lastTickTs).TotalMilliseconds);
        _pendingInvalidateTs = Stopwatch.GetTimestamp();
        MaybeReport();
    }

    /// <summary>Call at the top of <c>RenderOverride</c>. Returns the start stamp.</summary>
    public static long BeginPaint()
    {
        if (!Enabled) return 0;
        long now = Stopwatch.GetTimestamp();
        if (_lastPaintTs != 0)
            PaintInterval.Add(Stopwatch.GetElapsedTime(_lastPaintTs, now).TotalMilliseconds);
        _lastPaintTs = now;
        _paintsSinceTick++;
        if (_pendingInvalidateTs != 0)
        {
            TickToPaintLatency.Add(Stopwatch.GetElapsedTime(_pendingInvalidateTs, now).TotalMilliseconds);
            _pendingInvalidateTs = 0;
        }
        return now;
    }

    /// <summary>Call at the bottom of <c>RenderOverride</c> with the value from <see cref="BeginPaint"/>.</summary>
    public static void EndPaint(long startStamp)
    {
        if (!Enabled || startStamp == 0) return;
        PaintCost.Add(Stopwatch.GetElapsedTime(startStamp).TotalMilliseconds);
    }

    /// <summary>Write a one-off annotation into the same stream as the cadence report.</summary>
    public static void Note(string message)
    {
        if (!Enabled) return;
        Emit($"[note t={Clock.Elapsed.TotalSeconds,6:F1}s] {message}");
    }

    static void MaybeReport()
    {
        double t = Clock.Elapsed.TotalSeconds;
        if (t - _lastReportAt < ReportIntervalSeconds) return;
        double window = t - _lastReportAt;
        _lastReportAt = t;

        var sb = new StringBuilder(512);
        sb.Append("[cadence] t=").Append(t.ToString("F1")).Append("s  ")
          .Append("ticks=").Append(TickInterval.Count + 1)
          .Append('/').Append(window.ToString("F2")).Append("s ")
          .Append(TickInterval.Describe("interval"))
          .Append("  paints=").Append(PaintInterval.Count + 1).Append(' ')
          .Append(PaintInterval.Describe("interval"));
        sb.AppendLine();
        sb.Append("          ")
          .Append(TickCost.Describe("tickCost")).Append("  ")
          .Append(PaintCost.Describe("paintCost")).Append("  ")
          .Append(TickToPaintLatency.Describe("tick->paint"))
          .Append("  ticksWithNoPaint=").Append(_ticksWithoutPaint)
          .Append("  gpu=")
          .Append(GpuUsageSampler.Instance.Available
              ? (GpuUsageSampler.Instance.Latest * 100f).ToString("F0") + "%"
              : "n/a");

        // Fold in the in-paint breakdown so one log line set explains both
        // "how often" and "on what".
        var rows = new List<(string Label, double Ms)>(FrameProfiler.Snapshot());
        rows.Sort((a, b) => b.Ms.CompareTo(a.Ms));
        if (rows.Count > 0)
        {
            sb.AppendLine();
            sb.Append("          in-paint (EMA ms):");
            int shown = 0;
            foreach (var (label, ms) in rows)
            {
                if (ms < 0.05 && shown >= 8) continue;
                sb.Append(' ').Append(label).Append('=').Append(ms.ToString("F2"));
                if (++shown >= 14) break;
            }
        }

        Emit(sb.ToString());

        TickInterval.Reset();
        PaintInterval.Reset();
        TickCost.Reset();
        PaintCost.Reset();
        TickToPaintLatency.Reset();
        _ticksWithoutPaint = 0;
    }

    static void Emit(string line)
    {
        try { Console.Out.WriteLine(line); Console.Out.Flush(); } catch { /* no console */ }
        Debug.WriteLine(line);

        if (!_fileTried)
        {
            _fileTried = true;
            try
            {
                var path = Environment.GetEnvironmentVariable("UNOGALLERY_CADENCE_LOG")
                           ?? Path.Combine(Path.GetTempPath(), "unogallery-cadence.log");
                _file = new StreamWriter(path, append: false) { AutoFlush = true };
                _file.WriteLine($"# UnoGallery render cadence — started {DateTimeOffset.Now:O}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[cadence] log file unavailable: {ex.Message}");
            }
        }
        try { _file?.WriteLine(line); } catch { /* disk gone */ }
    }

    sealed class Sampler
    {
        double _sum, _max = double.MinValue, _min = double.MaxValue;
        public int Count { get; private set; }

        public void Add(double ms)
        {
            Count++;
            _sum += ms;
            if (ms > _max) _max = ms;
            if (ms < _min) _min = ms;
        }

        public void Reset()
        {
            Count = 0; _sum = 0; _max = double.MinValue; _min = double.MaxValue;
        }

        public string Describe(string name) => Count == 0
            ? $"{name}=n/a"
            : $"{name}[avg {_sum / Count,6:F2} min {_min,6:F2} max {_max,7:F2}]";
    }
}
