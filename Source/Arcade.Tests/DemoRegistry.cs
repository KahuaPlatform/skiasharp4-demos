using System.Reflection;

namespace Arcade.Tests;

/// <summary>
/// One entry per arcade-family demo. The types are named with <c>typeof</c> rather
/// than looked up by string, so the registry fails to COMPILE if a demo's
/// <c>GameWorld</c> or <c>Renderer</c> is renamed or dropped from the source-include
/// glob — a silently-skipped demo would be worse than a broken build.
/// </summary>
public sealed record DemoEntry(string Name, Type World, Type Renderer)
{
    public override string ToString() => Name;
}

public static class DemoRegistry
{
    public static readonly DemoEntry[] All =
    {
        new("Pohaku",   typeof(Pohaku.Game.GameWorld),   typeof(Pohaku.Game.Renderer)),
        new("HokuLele", typeof(HokuLele.Game.GameWorld), typeof(HokuLele.Game.Renderer)),
        new("Lua",      typeof(Lua.Game.GameWorld),      typeof(Lua.Game.Renderer)),
        new("Mahina",   typeof(Mahina.Game.GameWorld),   typeof(Mahina.Game.Renderer)),
        new("Heiau",    typeof(Heiau.Game.GameWorld),    typeof(Heiau.Game.Renderer)),
        new("Kanapi",   typeof(Kanapi.Game.GameWorld),   typeof(Kanapi.Game.Renderer)),
        new("Alaloa",   typeof(Alaloa.Game.GameWorld),   typeof(Alaloa.Game.Renderer)),
        new("Hahai",    typeof(Hahai.Game.GameWorld),    typeof(Hahai.Game.Renderer)),
        new("Paku",     typeof(Paku.Game.GameWorld),     typeof(Paku.Game.Renderer)),
        new("Kiai",     typeof(Kiai.Game.GameWorld),     typeof(Kiai.Game.Renderer)),
        new("Koa",      typeof(Koa.Game.GameWorld),      typeof(Koa.Game.Renderer)),
        new("Eli",      typeof(Eli.Game.GameWorld),      typeof(Eli.Game.Renderer)),
    };

    /// <summary>MSTest <c>[DynamicData]</c> source: one row per demo.</summary>
    public static IEnumerable<object[]> AllRows() => All.Select(d => new object[] { d });

    // --- Driving a demo generically -----------------------------------------
    //
    // The twelve worlds have the same shape but are unrelated types, so the shared
    // soak body reaches them by reflection. Every lookup below throws with the demo
    // name if it fails, so a renamed member surfaces as a loud, specific failure
    // rather than a quietly skipped demo.

    public static object CreateWorld(DemoEntry d)
    {
        var ctor = d.World.GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException($"{d.Name}: GameWorld has no parameterless constructor");
        return ctor.Invoke(null);
    }

    public static void Resize(DemoEntry d, object world, float w, float h)
    {
        var m = d.World.GetMethod("Resize", new[] { typeof(float), typeof(float) });
        // Not every demo needs a viewport: the fixed-Viewbox family scales one world
        // to the canvas and has no camera to size. Absent Resize is legitimate.
        m?.Invoke(world, new object[] { w, h });
    }

    public static void Update(DemoEntry d, object world, float dt)
    {
        var m = d.World.GetMethod("Update", new[] { typeof(float) })
            ?? throw new InvalidOperationException($"{d.Name}: no Update(float)");
        try { m.Invoke(world, new object[] { dt }); }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw tie.InnerException;   // report the real fault, not the reflection wrapper
        }
    }

    /// <summary>
    /// Enters the demo's self-playing loop. Every demo has one; Pohaku's predates the
    /// 4-state standard and calls it <c>StartDemo</c> rather than <c>StartAttract</c>.
    /// </summary>
    public static void StartAttract(DemoEntry d, object world)
    {
        var m = d.World.GetMethod("StartAttract", Type.EmptyTypes)
             ?? d.World.GetMethod("StartDemo", Type.EmptyTypes)
             ?? throw new InvalidOperationException($"{d.Name}: no StartAttract()/StartDemo()");
        try { m.Invoke(world, null); }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw tie.InnerException;
        }
    }

    /// <summary>The current mode as a boxed enum, or null if the demo exposes none.</summary>
    public static object? Mode(DemoEntry d, object world)
    {
        var p = d.World.GetProperty("Mode");
        if (p is not null) return p.GetValue(world);
        return d.World.GetField("Mode")?.GetValue(world);
    }

    public static void Render(DemoEntry d, object world, SkiaSharp.SKCanvas canvas, float w, float h)
    {
        var m = d.Renderer.GetMethod("Render", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{d.Name}: no static Renderer.Render");
        try { m.Invoke(null, new object?[] { canvas, world, w, h }); }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw tie.InnerException;
        }
    }

    // --- Numeric health ------------------------------------------------------

    /// <summary>
    /// Walks the world's public state looking for a NaN or infinity — the failure a
    /// long soak is really hunting, because one poisoned position spreads silently
    /// and only shows up as an entity that has vanished off-screen forever.
    /// Covers floats and <see cref="Vec2"/> directly on the world, and the same on
    /// the elements of any entity list it exposes.
    /// </summary>
    public static string? FindNonFiniteState(DemoEntry d, object world)
    {
        foreach (var m in d.World.GetFields(BindingFlags.Public | BindingFlags.Instance)
                                 .Cast<MemberInfo>()
                                 .Concat(d.World.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
        {
            object? value;
            Type type;
            switch (m)
            {
                case FieldInfo f: type = f.FieldType; value = f.GetValue(world); break;
                case PropertyInfo p when p.GetIndexParameters().Length == 0 && p.CanRead:
                    type = p.PropertyType;
                    try { value = p.GetValue(world); } catch { continue; }
                    break;
                default: continue;
            }

            if (Describe(type, value) is { } bad) return $"{d.Name}.{m.Name} {bad}";

            // Entity collections: check each element's own float/Vec2 members.
            if (value is System.Collections.IEnumerable seq && type != typeof(string))
            {
                int i = 0;
                foreach (var item in seq)
                {
                    if (item is null) continue;
                    var t = item.GetType();
                    foreach (var ef in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                        if (Describe(ef.FieldType, ef.GetValue(item)) is { } badItem)
                            return $"{d.Name}.{m.Name}[{i}].{ef.Name} {badItem}";
                    if (++i > 256) break;      // bounded: a soak can hold hundreds of entities
                }
            }
        }
        return null;

        // NaN is always corruption. Infinity is NOT, for a bare float: Hahai's
        // ChaseDurations ends in float.PositiveInfinity because the fourth chase
        // phase authentically never expires, so `timer -= dt` staying infinite is
        // the sentinel working as designed. Infinity in a Vec2 is a different
        // matter - no position or velocity is ever legitimately infinite - so it
        // stays a failure there.
        static string? Describe(Type t, object? v) => v switch
        {
            float f when float.IsNaN(f) => "is NaN",
            Vec2 p when float.IsNaN(p.X) || float.IsNaN(p.Y) => "is NaN",
            Vec2 p when float.IsInfinity(p.X) || float.IsInfinity(p.Y) => "is infinite",
            _ => null,
        };
    }
}
