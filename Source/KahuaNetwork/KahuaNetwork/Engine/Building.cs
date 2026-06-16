using System;
using System.Collections.Generic;
using System.Numerics;
using SkiaSharp;

namespace KahuaNetwork.Engine;

/// <summary>
/// One organization in the network, rendered as a glowing 3D tower. Holds its
/// role, world placement/size, and the live business metrics (active projects,
/// approval backlog, throughput, pending items) shown in the inspector panel.
/// </summary>
internal sealed class Building
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..6];
    public string Name { get; init; } = "Org";
    public OrgRole Role { get; init; } = OrgRole.Owner;
    public int ActiveProjects { get; init; } = 1;
    public int PendingApprovals { get; set; }
    public int DocsThisWeek { get; init; }
    public string Phase { get; set; } = "Active";
    public Vector3 GroundCenter { get; set; }
    public float Width { get; set; } = 40;
    public float Depth { get; set; } = 40;
    public float Height { get; set; } = 120;
    public SKColor BaseColor { get; set; } = Theme.Cyan;
    public double Risk { get; set; } = 0.2;
    public double Completion { get; set; } = 0.4;
    public double TelemetryPhase { get; set; }
    public double TelemetrySpeed { get; set; } = 1.0;
    public bool IsSelected { get; set; }
    public float ExpandProgress { get; set; } = 0f; // 0 = collapsed, 1 = expanded
    public float HoverIntensity { get; set; } = 0f;
    public List<double> History { get; } = new();

    public Vector3 TopCenter => GroundCenter + new Vector3(0, Height * (0.5f + ExpandProgress * 0.6f), 0);
    public Vector3 ApexCenter => GroundCenter + new Vector3(0, Height * (1.0f + ExpandProgress * 1.2f), 0);

    public double Pulse(double timeSeconds)
    {
        return 0.5 + 0.5 * Math.Sin((timeSeconds + TelemetryPhase) * TelemetrySpeed * 2.0);
    }
}
