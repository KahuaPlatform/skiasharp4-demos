using System;
using System.Collections.Generic;

namespace KahuaNetwork.Engine;

/// <summary>
/// The rotating "AI" narration feed: periodically emits network-flow insights
/// (routed RFIs, trending turnaround, auto-route confirmations) shown in the HUD
/// activity panel to convey that intelligence is observing the network.
/// </summary>
internal sealed class AIInsightFeed
{
    private readonly Random _rng = new();
    private readonly Queue<Insight> _recent = new();
    private double _nextEmit;

    public IReadOnlyCollection<Insight> Recent => _recent;

    // Templates phrased in Kahua Network terms.
    // {a} = source org, {b} = destination org, {n} = number, {role} = role
    public static readonly string[] Templates =
    {
        "RFI #{n} routed from {a} to {b}. SLA 48h.",
        "{a} submitted Pay App #{n} to {b}. Auto-routed for approval.",
        "Submittal package returned to {a} — markups from {b}.",
        "Change Order #{n} from {a} acknowledged by {b}.",
        "Daily Report from {a} synced to {b} — single entry, all parties updated.",
        "Punch list issued by {b} to {a}: {n} items.",
        "Drawing rev {n} published by {a} — circulated to network.",
        "RFI #{n} answered by {b} in 6h — well under SLA.",
        "Pay App #{n} approved at {b}. Funds release queued.",
        "{a} joined the Kahua Network — connected to {b}.",
        "Bid package from {a} reached {n} subs in 12 minutes.",
        "Safety data sheet from {a} synced across {n} projects.",
        "Submittal turnaround at {b} trending 31% faster this quarter.",
    };

    public void Update(double dt, City city)
    {
        _nextEmit -= dt;
        if (_nextEmit <= 0)
        {
            _nextEmit = 1.8 + _rng.NextDouble() * 2.4;
            if (city.Buildings.Count < 2) return;
            var a = city.Buildings[_rng.Next(city.Buildings.Count)];
            Building b = a;
            int guard = 0;
            while (b == a && guard++ < 6)
                b = city.Buildings[_rng.Next(city.Buildings.Count)];
            int n = 1000 + _rng.Next(8999);
            var text = Templates[_rng.Next(Templates.Length)]
                .Replace("{a}", a.Name)
                .Replace("{b}", b.Name)
                .Replace("{n}", n.ToString())
                .Replace("{role}", a.Role.Display());
            Push(new Insight(text, DateTime.UtcNow, Classify(text), a));
        }
    }

    public void Push(Insight i)
    {
        _recent.Enqueue(i);
        while (_recent.Count > 5) _recent.Dequeue();
    }

    private static InsightKind Classify(string text)
    {
        if (text.Contains("approv", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("faster", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("under SLA", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("joined", StringComparison.OrdinalIgnoreCase))
            return InsightKind.Win;
        if (text.Contains("overdue", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("breach", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("escalat", StringComparison.OrdinalIgnoreCase))
            return InsightKind.Risk;
        return InsightKind.Info;
    }
}

/// <summary>The flavor of an insight (drives its accent color in the feed).</summary>
internal enum InsightKind
{
    Info,
    Risk,
    Win,
}

/// <summary>One feed entry: its text, timestamp, kind, and optional originating org.</summary>
internal sealed record Insight(string Text, DateTime At, InsightKind Kind, Building? Site);
