using System.Diagnostics;

#pragma warning disable CA1416 // PerformanceCounter is Windows-only — guarded by HAS_PERFCOUNTERS

namespace UnoGallery.Diagnostics;

/// <summary>
/// System GPU utilisation, sampled on a background thread.
///
/// Why this class exists: the "GPU Engine \ Utilization Percentage"
/// perf-counter category carries one instance per (process, adapter, engine)
/// triple — 907 of them on a busy desktop, 346 of which are engtype_3D. Every
/// <see cref="PerformanceCounter.NextValue"/> is its own PDH query, so summing
/// all of them costs ~385 ms, and <see cref="PerformanceCounterCategory.GetInstanceNames"/>
/// alone costs ~1.6 s. <c>GpuMonitorTile</c> used to do all of that inline in
/// <c>RenderOverride</c> every 0.5 s, which held the render loop at ~8 fps with
/// a ~385 ms hard stall twice a second.
///
/// Two changes make it nearly free:
///
///   - <b>Off the UI thread.</b> Sampling runs on a background thread; the UI
///     thread only does a <see cref="Volatile.Read(ref float)"/> of the last
///     published value.
///   - <b>Poll a hot set.</b> An instance reading 0 % contributes nothing to the
///     sum, and the overwhelming majority are idle or exited processes. Between
///     full passes we poll only the instances that were actually busy (plus our
///     own process, so a graphics demo always sees itself). A full pass —
///     re-enumerate, read everything, re-pick the hot set — runs once a minute
///     to catch newly-busy processes. The resulting lag is at most one minute
///     before an unrelated process shows up in the trace, which is invisible on
///     a scrolling 80-sample readout.
///
/// Counters are reused across full passes rather than recreated, so the rate
/// counters keep their sampling baseline.
/// </summary>
public sealed class GpuUsageSampler
{
    public static GpuUsageSampler Instance { get; } = new();

    float _latest;
    int _availability;   // 0 = still starting up, 1 = live, 2 = unavailable
    int _started;

    /// <summary>Most recent system GPU utilisation, 0..1. Cheap to read from the UI thread.</summary>
    public float Latest => Volatile.Read(ref _latest);

    /// <summary>False until the first sample lands, and permanently false if the counters can't be read.</summary>
    public bool Available => Volatile.Read(ref _availability) == 1;

    /// <summary>Idempotent; safe to call from every frame.</summary>
    public void EnsureStarted()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return;
#if HAS_PERFCOUNTERS
        new Thread(SampleLoop)
        {
            IsBackground = true,
            Name = "gpu-usage-sampler",
            // The thing we are measuring is frame rendering. Never compete with it.
            Priority = ThreadPriority.BelowNormal,
        }.Start();
#else
        Volatile.Write(ref _availability, 2);
#endif
    }

#if HAS_PERFCOUNTERS
    const string CategoryName = "GPU Engine";
    const string CounterName = "Utilization Percentage";
    const string EngineFilter = "engtype_3D";
    const int MaxHotCounters = 128;

    static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    static readonly TimeSpan FullPassInterval = TimeSpan.FromSeconds(60);

    readonly List<PerformanceCounter> _all = new();
    readonly List<PerformanceCounter> _hot = new();
    readonly string _ownPrefix = $"pid_{Environment.ProcessId}_";

    double _lastPollMs;

    void SampleLoop()
    {
        var sinceFullPass = new Stopwatch();
        while (true)
        {
            try
            {
                if (!sinceFullPass.IsRunning || sinceFullPass.Elapsed >= FullPassInterval)
                {
                    FullPass();
                    sinceFullPass.Restart();
                }
                else
                {
                    var sw = Stopwatch.StartNew();
                    Publish(Sum(_hot));
                    _lastPollMs = sw.Elapsed.TotalMilliseconds;
                }
            }
            catch (Exception ex)
            {
                // Perf counters are a best-effort nicety — a disabled or corrupt
                // PDH registry shouldn't take the app's render loop with it.
                RenderCadence.Note($"[gpu-sampler] stopped: {ex.GetType().Name}: {ex.Message}");
                Volatile.Write(ref _availability, 2);
                return;
            }

            Thread.Sleep(PollInterval);
        }
    }

    /// <summary>Re-enumerate instances, read every counter, and re-pick the hot set.</summary>
    void FullPass()
    {
        // Clear first — Rediscover disposes counters whose process has exited,
        // and _hot must not be left holding those.
        _hot.Clear();

        var sw = Stopwatch.StartNew();
        Rediscover();
        double discoverMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        var readings = new List<(PerformanceCounter Counter, float Value)>(_all.Count);
        float total = 0f;
        foreach (var c in _all)
        {
            float v = Read(c);
            total += v;
            readings.Add((c, v));
        }
        double readMs = sw.Elapsed.TotalMilliseconds;

        readings.Sort((a, b) => b.Value.CompareTo(a.Value));

        // Our own process first so the cap can never evict it.
        foreach (var (counter, _) in readings)
            if (IsOwnProcess(counter.InstanceName)) _hot.Add(counter);

        foreach (var (counter, value) in readings)
        {
            if (_hot.Count >= MaxHotCounters) break;
            if (value <= 0f) break;   // sorted descending — everything after this is idle
            if (!IsOwnProcess(counter.InstanceName)) _hot.Add(counter);
        }

        Publish(total);

        RenderCadence.Note(
            $"[gpu-sampler] full pass: {_all.Count} counters ({discoverMs:F1} ms enumerate, "
            + $"{readMs:F1} ms read) -> hot set {_hot.Count}; last hot poll {_lastPollMs:F1} ms");
    }

    void Rediscover()
    {
        var instances = new PerformanceCounterCategory(CategoryName).GetInstanceNames();

        var wanted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var inst in instances)
            if (inst.Contains(EngineFilter, StringComparison.Ordinal))
                wanted.Add(inst);

        // Keep counters whose instance is still present; drop the rest. Removing
        // each survivor from `wanted` leaves only genuinely new instances behind.
        for (int i = _all.Count - 1; i >= 0; i--)
        {
            if (wanted.Remove(_all[i].InstanceName)) continue;
            _all[i].Dispose();
            _all.RemoveAt(i);
        }

        foreach (var inst in wanted)
        {
            var c = new PerformanceCounter(CategoryName, CounterName, inst, readOnly: true);
            _ = Read(c);   // prime — a rate counter's first read is always 0
            _all.Add(c);
        }

        if (_all.Count == 0)
            throw new InvalidOperationException($"no '{EngineFilter}' instances in '{CategoryName}'");
    }

    static float Sum(List<PerformanceCounter> counters)
    {
        float total = 0f;
        foreach (var c in counters) total += Read(c);
        return total;
    }

    static float Read(PerformanceCounter c)
    {
        // A process can exit between enumeration and read; that's a dead
        // instance, not a failure of the category.
        try { return c.NextValue(); }
        catch { return 0f; }
    }

    bool IsOwnProcess(string instanceName) =>
        instanceName.StartsWith(_ownPrefix, StringComparison.Ordinal);

    void Publish(float percent)
    {
        Volatile.Write(ref _latest, Math.Clamp(percent / 100f, 0f, 1f));
        Volatile.Write(ref _availability, 1);
    }
#endif
}
