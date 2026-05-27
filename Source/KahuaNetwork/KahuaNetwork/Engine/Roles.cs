using SkiaSharp;

namespace KahuaNetwork.Engine;

internal enum OrgRole
{
    Owner,
    GeneralContractor,
    Subcontractor,
    Architect,
    ConstructionManager,
    ProgramManager,
}

internal static class Roles
{
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

internal static class Documents
{
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
