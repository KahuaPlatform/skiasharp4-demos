using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace KahuaNetwork.Engine;

// Despite the legacy name, "City" now represents the Kahua Network:
// a constellation of connected organizations and the documents they exchange.
internal sealed class City
{
    public List<Building> Buildings { get; } = new();
    public List<DataStream> DataStreams { get; } = new();

    // Realistic-feeling firm name fragments per role
    private static readonly string[] OwnerNames =
    {
        "Meridian Holdings", "North Harbor Authority", "Stellar Health System",
        "Crescent University", "Polaris Transit", "Pinnacle Realty Trust",
        "Coastal Power & Light", "Atlas Logistics", "Granite School District",
    };
    private static readonly string[] GcNames =
    {
        "Beacon Construction", "Halcyon Builders", "Ironclad Construction",
        "Summit Construction Group", "Westline Builders", "Apex Construction",
        "Cornerstone Builders", "Vanguard Construction",
    };
    private static readonly string[] SubNames =
    {
        "Forge Steel & Erection", "Lumen Electric", "Cascade Mechanical",
        "Stratus Glazing", "Bedrock Concrete", "Coil Sheet Metal",
        "Vertex Plumbing", "Helios Roofing", "Switchback Drywall",
        "Orbit Fire Protection", "Ridgeway Excavation",
    };
    private static readonly string[] ArchNames =
    {
        "Studio Cipher", "Northwall Architects", "Lyra Design Collective",
        "Onyx + Partners", "Prism Studio", "Aether Architecture",
    };
    private static readonly string[] CmNames =
    {
        "Crucible CM", "Meridian PM Services", "Sentinel Construction Mgmt",
        "Keystone Advisory",
    };
    private static readonly string[] PmNames =
    {
        "Atlas Program Group", "Concourse PM", "Helios Program Office",
    };

    private static readonly string[] PhasesByRole =
    {
        "Active · Routing", "Active · Reviewing", "Active · Issuing",
        "Active · Approving", "Active · Coordinating", "Active · Submitting",
    };

    public static City Generate(int seed = 42, int gridSize = 6)
    {
        var rng = new Random(seed);
        var net = new City();

        float spacing = 130f;
        float centerOffset = -(gridSize - 1) / 2f * spacing;

        for (int gx = 0; gx < gridSize; gx++)
        {
            for (int gz = 0; gz < gridSize; gz++)
            {
                if (rng.NextDouble() < 0.18 && !(gx == gridSize / 2 && gz == gridSize / 2)) continue;

                // Assign a role with a realistic mix
                OrgRole role = PickRole(rng);

                float dx = gx - (gridSize - 1) / 2f;
                float dz = gz - (gridSize - 1) / 2f;
                float distFromCenter = MathF.Sqrt(dx * dx + dz * dz);

                // Owners cluster centrally and are tallest; subs are shorter and spread
                float w = 50f + (float)rng.NextDouble() * 30f;
                float d = 50f + (float)rng.NextDouble() * 30f;
                float baseH = role switch
                {
                    OrgRole.Owner => 220f + (float)rng.NextDouble() * 220f,
                    OrgRole.GeneralContractor => 180f + (float)rng.NextDouble() * 180f,
                    OrgRole.ConstructionManager => 150f + (float)rng.NextDouble() * 140f,
                    OrgRole.Architect => 140f + (float)rng.NextDouble() * 120f,
                    OrgRole.ProgramManager => 200f + (float)rng.NextDouble() * 140f,
                    _ => 90f + (float)rng.NextDouble() * 120f, // Subs
                };
                baseH *= 1.0f + MathF.Max(0, 1.5f - distFromCenter) * 0.3f;

                var name = PickName(role, rng);

                var org = new Building
                {
                    Name = name,
                    Role = role,
                    Phase = PhasesByRole[rng.Next(PhasesByRole.Length)],
                    GroundCenter = new Vector3(
                        centerOffset + gx * spacing + (float)(rng.NextDouble() - 0.5) * 18f,
                        0,
                        centerOffset + gz * spacing + (float)(rng.NextDouble() - 0.5) * 18f),
                    Width = w,
                    Depth = d,
                    Height = baseH,
                    BaseColor = role.Color(),
                    // "Risk" now reads as backlog / overdue pressure on this org
                    Risk = Math.Pow(rng.NextDouble(), 1.8),
                    // "Completion" reads as % of projects on-track for this org
                    Completion = 0.4 + rng.NextDouble() * 0.55,
                    TelemetryPhase = rng.NextDouble() * Math.PI * 2,
                    TelemetrySpeed = 0.4 + rng.NextDouble() * 1.2,
                    ActiveProjects = 1 + rng.Next(role == OrgRole.Subcontractor ? 4 : 9),
                    PendingApprovals = rng.Next(0, 14),
                    DocsThisWeek = 5 + rng.Next(120),
                };
                net.Buildings.Add(org);
            }
        }

        // Wire exchanges — bias toward realistic flows:
        //  RFIs:        Sub/GC ↔ Architect
        //  Submittals:  GC ↔ Architect, Sub ↔ GC
        //  Pay Apps:    GC ↔ Owner/CM, Sub ↔ GC
        //  Change Order: GC ↔ Owner
        //  Daily Report: Sub/GC → CM/Owner
        int exchangeCount = Math.Min(net.Buildings.Count * 2, 36);
        for (int i = 0; i < exchangeCount; i++)
        {
            var kind = (DocumentKind)rng.Next(Enum.GetValues<DocumentKind>().Length);
            var (a, b) = PickEndpointsFor(kind, net.Buildings, rng);
            if (a == b || a == null || b == null) continue;
            net.DataStreams.Add(new DataStream
            {
                From = a!,
                To = b!,
                Kind = kind,
                Color = kind.Color(),
                Speed = 0.3f + (float)rng.NextDouble() * 0.7f,
                Phase = (float)rng.NextDouble(),
                Thickness = 1.2f + (float)rng.NextDouble() * 1.6f,
            });
        }

        return net;
    }

    private static OrgRole PickRole(Random rng)
    {
        double r = rng.NextDouble();
        return r switch
        {
            < 0.42 => OrgRole.Subcontractor,
            < 0.62 => OrgRole.GeneralContractor,
            < 0.76 => OrgRole.Architect,
            < 0.88 => OrgRole.Owner,
            < 0.96 => OrgRole.ConstructionManager,
            _ => OrgRole.ProgramManager,
        };
    }

    private static string PickName(OrgRole role, Random rng)
    {
        var pool = role switch
        {
            OrgRole.Owner => OwnerNames,
            OrgRole.GeneralContractor => GcNames,
            OrgRole.Subcontractor => SubNames,
            OrgRole.Architect => ArchNames,
            OrgRole.ConstructionManager => CmNames,
            OrgRole.ProgramManager => PmNames,
            _ => OwnerNames,
        };
        return pool[rng.Next(pool.Length)];
    }

    private static (Building? a, Building? b) PickEndpointsFor(
        DocumentKind kind, List<Building> all, Random rng)
    {
        Building? PickAny(OrgRole role) =>
            all.Where(b => b.Role == role).OrderBy(_ => rng.Next()).FirstOrDefault();

        switch (kind)
        {
            case DocumentKind.RFI:
            {
                var src = PickAny(OrgRole.Subcontractor) ?? PickAny(OrgRole.GeneralContractor);
                var dst = PickAny(OrgRole.Architect) ?? PickAny(OrgRole.GeneralContractor);
                return (src, dst);
            }
            case DocumentKind.Submittal:
            {
                var src = PickAny(OrgRole.Subcontractor) ?? PickAny(OrgRole.GeneralContractor);
                var dst = PickAny(OrgRole.Architect) ?? PickAny(OrgRole.GeneralContractor);
                return (src, dst);
            }
            case DocumentKind.PayApp:
            {
                var src = PickAny(OrgRole.GeneralContractor) ?? PickAny(OrgRole.Subcontractor);
                var dst = PickAny(OrgRole.Owner) ?? PickAny(OrgRole.ConstructionManager)
                          ?? PickAny(OrgRole.GeneralContractor);
                return (src, dst);
            }
            case DocumentKind.ChangeOrder:
            {
                var src = PickAny(OrgRole.GeneralContractor);
                var dst = PickAny(OrgRole.Owner) ?? PickAny(OrgRole.ProgramManager);
                return (src, dst);
            }
            case DocumentKind.DailyReport:
            {
                var src = PickAny(OrgRole.Subcontractor) ?? PickAny(OrgRole.GeneralContractor);
                var dst = PickAny(OrgRole.ConstructionManager) ?? PickAny(OrgRole.Owner)
                          ?? PickAny(OrgRole.GeneralContractor);
                return (src, dst);
            }
            case DocumentKind.PunchList:
            {
                var src = PickAny(OrgRole.Architect) ?? PickAny(OrgRole.ConstructionManager);
                var dst = PickAny(OrgRole.GeneralContractor) ?? PickAny(OrgRole.Subcontractor);
                return (src, dst);
            }
            case DocumentKind.Drawing:
            default:
            {
                var src = PickAny(OrgRole.Architect);
                var dst = PickAny(OrgRole.GeneralContractor) ?? PickAny(OrgRole.Subcontractor);
                return (src, dst);
            }
        }
    }
}
