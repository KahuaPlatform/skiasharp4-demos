using SkiaSharp;

namespace KahuaNetwork.Engine;

/// <summary>The kinds of organization (node) in the Kahua Network.</summary>
internal enum OrgRole
{
    Owner,
    GeneralContractor,
    Subcontractor,
    Architect,
    ConstructionManager,
    ProgramManager,
}

/// <summary>Extension helpers mapping an <see cref="OrgRole"/> to its color, short tag, and display name.</summary>
internal static class Roles
{
    /// <summary>The role's accent/glow color.</summary>
    public static SKColor Color(this OrgRole r) => r switch
    {
        OrgRole.Owner => Theme.Cyan,
        OrgRole.GeneralContractor => Theme.Magenta,
        OrgRole.Subcontractor => Theme.Lime,
        OrgRole.Architect => Theme.Violet,
        OrgRole.ConstructionManager => Theme.Amber,
        OrgRole.ProgramManager => new SKColor(0x7B, 0xC9, 0xFF),
        _ => Theme.Cyan,
    };

    /// <summary>The short chip tag (e.g. "GC", "SUB").</summary>
    public static string Tag(this OrgRole r) => r switch
    {
        OrgRole.Owner => "OWNER",
        OrgRole.GeneralContractor => "GC",
        OrgRole.Subcontractor => "SUB",
        OrgRole.Architect => "ARCH",
        OrgRole.ConstructionManager => "CM",
        OrgRole.ProgramManager => "PM",
        _ => "?",
    };

    /// <summary>The full human-readable role name.</summary>
    public static string Display(this OrgRole r) => r switch
    {
        OrgRole.Owner => "Owner",
        OrgRole.GeneralContractor => "General Contractor",
        OrgRole.Subcontractor => "Subcontractor",
        OrgRole.Architect => "Architect / Designer",
        OrgRole.ConstructionManager => "Construction Manager",
        OrgRole.ProgramManager => "Program Manager",
        _ => "Member",
    };
}

/// <summary>The document types that flow between organizations as data streams.</summary>
internal enum DocumentKind
{
    RFI,
    Submittal,
    PayApp,
    ChangeOrder,
    DailyReport,
    PunchList,
    Drawing,
}

/// <summary>Extension helpers mapping a <see cref="DocumentKind"/> to its glow color and label.</summary>
internal static class Documents
{
    /// <summary>The exchange's glow/pulse color.</summary>
    public static SKColor Color(this DocumentKind k) => k switch
    {
        DocumentKind.RFI => Theme.Cyan,
        DocumentKind.Submittal => Theme.Violet,
        DocumentKind.PayApp => Theme.Lime,
        DocumentKind.ChangeOrder => Theme.Amber,
        DocumentKind.DailyReport => new SKColor(0x9B, 0xD8, 0xFF),
        DocumentKind.PunchList => Theme.Magenta,
        DocumentKind.Drawing => new SKColor(0xC8, 0xA8, 0xFF),
        _ => Theme.Cyan,
    };

    /// <summary>The uppercase label shown on the stream/HUD (e.g. "PAY APP").</summary>
    public static string ShortLabel(this DocumentKind k) => k switch
    {
        DocumentKind.RFI => "RFI",
        DocumentKind.Submittal => "SUBMITTAL",
        DocumentKind.PayApp => "PAY APP",
        DocumentKind.ChangeOrder => "CHANGE ORDER",
        DocumentKind.DailyReport => "DAILY REPORT",
        DocumentKind.PunchList => "PUNCH LIST",
        DocumentKind.Drawing => "DRAWING",
        _ => "DOC",
    };
}
