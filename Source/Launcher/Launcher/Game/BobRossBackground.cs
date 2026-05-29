using System;
using SkiaSharp;

namespace Launcher.Game;

// Bob Ross-style Hawaiian sunset: a soft pastel sky gradient that warms toward
// the horizon, a hazy sun disc with a wide halo, layered distant mountain
// silhouettes that get cooler as they recede, gentle ocean bands with happy
// little reflective ripples on top, and two palm-tree silhouettes framing the
// foreground. Painted in canvas-pixel coordinates (cw, ch) so it fills the
// window regardless of world scale.
public static class BobRossBackground
{
    static readonly SKColor SkyTop      = new(0x4A, 0x3A, 0x82);   // dusky lavender
    static readonly SKColor SkyMid      = new(0xEA, 0x95, 0x7C);   // peach
    static readonly SKColor SkyLow      = new(0xFF, 0xD9, 0x9A);   // butter yellow
    static readonly SKColor SunCore     = new(0xFF, 0xF0, 0xC8);
    static readonly SKColor SunHalo     = new(0xFF, 0xCC, 0x88, 0x80);
    static readonly SKColor MountainBack  = new(0x6A, 0x52, 0x9E, 0xC8);
    static readonly SKColor MountainMid   = new(0x4F, 0x3D, 0x7A, 0xE0);
    static readonly SKColor MountainFront = new(0x2F, 0x24, 0x4A);
    static readonly SKColor OceanFar    = new(0x2E, 0x6E, 0x8E);
    static readonly SKColor OceanMid    = new(0x4F, 0xB0, 0xC2);
    static readonly SKColor OceanNear   = new(0x88, 0xDD, 0xDC);
    static readonly SKColor RippleColor = new(0xFF, 0xFF, 0xFF, 0xA0);
    static readonly SKColor PalmColor   = new(0x1C, 0x16, 0x2E);

    public static void Draw(SKCanvas c, float cw, float ch)
    {
        DrawSky(c, cw, ch);
        DrawClouds(c, cw, ch);
        DrawSun(c, cw, ch);
        DrawMountains(c, cw, ch);
        DrawOcean(c, cw, ch);
        DrawSunReflection(c, cw, ch);   // golden trail painted on top of ocean
        DrawPalms(c, cw, ch);
    }

    // Three or four happy little cumulus clouds drifting in the upper sky.
    // Each cloud is a stack of soft white blurred circles so the edges feather
    // into the lavender / peach gradient like wet-into-wet acrylic.
    static void DrawClouds(SKCanvas c, float cw, float ch)
    {
        var clouds = new (float cx, float cy, float scale)[]
        {
            (cw * 0.18f, ch * 0.10f, 1.0f),
            (cw * 0.42f, ch * 0.07f, 1.4f),
            (cw * 0.80f, ch * 0.12f, 0.9f),
        };
        foreach (var cl in clouds) DrawCloud(c, cl.cx, cl.cy, MathF.Min(cw, ch) * 0.04f * cl.scale);
    }

    static void DrawCloud(SKCanvas c, float cx, float cy, float r)
    {
        // Soft halo first so the cloud bleeds into the sky.
        using (var halo = new SKPaint
        {
            IsAntialias = true,
            Style       = SKPaintStyle.Fill,
            Color       = new SKColor(0xFF, 0xFF, 0xFF, 0x60),
            MaskFilter  = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 14f),
        })
        {
            c.DrawCircle(cx, cy, r * 3.0f, halo);
        }
        // Five overlapping puffs forming a cumulus shape.
        using var puff = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xFF, 0xFD, 0xF2, 0xE0) };
        c.DrawCircle(cx - r * 1.4f, cy + r * 0.3f, r * 1.1f, puff);
        c.DrawCircle(cx - r * 0.5f, cy - r * 0.4f, r * 1.4f, puff);
        c.DrawCircle(cx + r * 0.4f, cy - r * 0.2f, r * 1.2f, puff);
        c.DrawCircle(cx + r * 1.4f, cy + r * 0.2f, r * 1.0f, puff);
        c.DrawCircle(cx,            cy + r * 0.5f, r * 1.0f, puff);
    }

    // Bright golden-orange column directly below the sun, gradient-faded so
    // it reads as reflected sunlight on water. Bob Ross adds short horizontal
    // dashes of pure white over the brightest part.
    static void DrawSunReflection(SKCanvas c, float cw, float ch)
    {
        float horizonY = ch * 0.58f;
        float sunX = cw * 0.62f;
        float topY = horizonY;
        float botY = ch - 8f;
        float topHalfW = ch * 0.025f;
        float botHalfW = ch * 0.12f;
        using var pb = new SKPathBuilder();
        pb.MoveTo(sunX - topHalfW, topY);
        pb.LineTo(sunX + topHalfW, topY);
        pb.LineTo(sunX + botHalfW, botY);
        pb.LineTo(sunX - botHalfW, botY);
        pb.Close();
        using var path = pb.Detach();
        using var goldGradient = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, topY), new SKPoint(0, botY),
                new[]
                {
                    new SKColor(0xFF, 0xE6, 0x9A, 0xD0),
                    new SKColor(0xFF, 0xC6, 0x6A, 0x80),
                    new SKColor(0xFF, 0xA8, 0x4A, 0x20),
                },
                new[] { 0f, 0.5f, 1f },
                SKShaderTileMode.Clamp),
        };
        c.DrawPath(path, goldGradient);

        // Short bright dashes over the column — the Bob Ross "specular highlight"
        // brushstrokes. Random-but-stable via fixed seed.
        var rng = new Random(101);
        using var dash = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.4f, Color = new SKColor(0xFF, 0xFF, 0xF0, 0xC0), StrokeCap = SKStrokeCap.Round };
        for (int i = 0; i < 12; i++)
        {
            float t = (i + 1) / 13f;
            float y = topY + t * (botY - topY);
            float halfW = topHalfW + (botHalfW - topHalfW) * t;
            float xCenter = sunX + ((float)rng.NextDouble() - 0.5f) * halfW * 1.2f;
            float dashLen = halfW * (0.4f + (float)rng.NextDouble() * 0.5f);
            c.DrawLine(xCenter - dashLen / 2f, y, xCenter + dashLen / 2f, y, dash);
        }
    }

    static void DrawSky(SKCanvas c, float cw, float ch)
    {
        float horizonY = ch * 0.58f;
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, horizonY),
                new[] { SkyTop, SkyMid, SkyLow },
                new[] { 0f, 0.65f, 1f },
                SKShaderTileMode.Clamp),
        };
        c.DrawRect(new SKRect(0, 0, cw, horizonY), paint);
    }

    static void DrawSun(SKCanvas c, float cw, float ch)
    {
        float horizonY = ch * 0.58f;
        float sunX = cw * 0.62f;
        float sunY = horizonY - 18f;     // sits just above horizon
        float coreR = MathF.Min(cw, ch) * 0.06f;

        // Wide soft halo built from three diminishing alpha discs so the sun
        // bleeds into the sky like wet acrylic.
        using (var halo = new SKPaint
        {
            IsAntialias = true,
            Style       = SKPaintStyle.Fill,
            Color       = SunHalo,
            MaskFilter  = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 26f),
        })
        {
            c.DrawCircle(sunX, sunY, coreR * 3.6f, halo);
        }
        using (var halo2 = new SKPaint
        {
            IsAntialias = true,
            Style       = SKPaintStyle.Fill,
            Color       = SunHalo.WithAlpha(0xC0),
            MaskFilter  = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12f),
        })
        {
            c.DrawCircle(sunX, sunY, coreR * 1.8f, halo2);
        }
        using (var core = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SunCore })
        {
            c.DrawCircle(sunX, sunY, coreR, core);
        }
    }

    static void DrawMountains(SKCanvas c, float cw, float ch)
    {
        float horizonY = ch * 0.58f;
        // Three layers receding into the distance. The back-most gets snow caps
        // painted over its peaks — the signature Bob Ross "almighty mountain".
        DrawMountainLayer(c, cw, horizonY, peakH: ch * 0.14f, jitter: 0.6f, color: MountainBack,  seed: 7,  snow: true);
        DrawMountainLayer(c, cw, horizonY, peakH: ch * 0.11f, jitter: 0.7f, color: MountainMid,   seed: 13, snow: false);
        DrawMountainLayer(c, cw, horizonY, peakH: ch * 0.07f, jitter: 0.9f, color: MountainFront, seed: 21, snow: false);
    }

    static void DrawMountainLayer(SKCanvas c, float cw, float horizonY, float peakH, float jitter, SKColor color, int seed, bool snow)
    {
        var rng = new Random(seed);
        const int segments = 12;
        var peaks = new (float x, float y)[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float x = (i / (float)segments) * cw;
            float h = peakH * (0.5f + (float)rng.NextDouble() * jitter);
            peaks[i] = (x, horizonY - h);
        }

        // Mountain silhouette.
        using (var pb = new SKPathBuilder())
        {
            pb.MoveTo(-10f, horizonY + 4f);
            foreach (var (x, y) in peaks) pb.LineTo(x, y);
            pb.LineTo(cw + 10f, horizonY + 4f);
            pb.Close();
            using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = color };
            using var path = pb.Detach();
            c.DrawPath(path, paint);
        }

        if (!snow) return;

        // Snow caps — a small white triangle on each peak, slightly asymmetric
        // (longer on the shadow side) so they look painted rather than stamped.
        using var snowPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xFA, 0xF6, 0xF0, 0xF0) };
        for (int i = 1; i < segments; i++)
        {
            var (px, py) = peaks[i];
            // Only cap peaks that are local maxima (lower y than both neighbors).
            if (peaks[i - 1].y < py || peaks[i + 1].y < py) continue;
            float capH = (horizonY - py) * 0.30f;
            float capWLeft  = (px - peaks[i - 1].x) * 0.18f;
            float capWRight = (peaks[i + 1].x - px) * 0.22f;
            using var pb = new SKPathBuilder();
            pb.MoveTo(px - capWLeft,  py + capH);
            pb.LineTo(px,             py - 2f);
            pb.LineTo(px + capWRight, py + capH * 0.85f);
            // Wavy "snow line" base instead of a clean horizontal — slight zigzag.
            pb.LineTo(px + capWRight * 0.4f, py + capH * 1.15f);
            pb.LineTo(px - capWLeft  * 0.4f, py + capH * 1.05f);
            pb.Close();
            using var path = pb.Detach();
            c.DrawPath(path, snowPaint);
        }
    }

    static void DrawOcean(SKCanvas c, float cw, float ch)
    {
        float horizonY = ch * 0.58f;
        using var oceanPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, horizonY), new SKPoint(0, ch),
                new[] { OceanFar, OceanMid, OceanNear },
                new[] { 0f, 0.45f, 1f },
                SKShaderTileMode.Clamp),
        };
        c.DrawRect(new SKRect(0, horizonY, cw, ch), oceanPaint);

        // "Happy little waves" — soft horizontal highlights with slight wobble
        // that suggest reflected sun on the surface. Drawn brighter and shorter
        // as we go down the canvas (waves nearer the viewer = bigger ripples).
        using var ripple = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = RippleColor };
        for (int i = 0; i < 14; i++)
        {
            float t = i / 14f;
            float y = horizonY + 6f + t * (ch - horizonY - 18f);
            float widthFrac = 0.10f + t * 0.45f;
            float startX = cw * (0.5f - widthFrac);
            float endX   = cw * (0.5f + widthFrac);
            // Slight wobble for the painted look.
            using var pb = new SKPathBuilder();
            pb.MoveTo(startX, y);
            int steps = 6;
            for (int s = 1; s <= steps; s++)
            {
                float xs = startX + (endX - startX) * (s / (float)steps);
                float ys = y + ((s & 1) == 0 ? -1.5f : 1.5f);
                pb.LineTo(xs, ys);
            }
            using var path = pb.Detach();
            c.DrawPath(path, ripple);
        }
    }

    static void DrawPalms(SKCanvas c, float cw, float ch)
    {
        DrawPalm(c, cw * 0.08f, ch * 0.92f, height: ch * 0.55f, lean: -0.20f);
        DrawPalm(c, cw * 0.93f, ch * 0.94f, height: ch * 0.62f, lean:  0.15f);
    }

    static void DrawPalm(SKCanvas c, float baseX, float baseY, float height, float lean)
    {
        using var trunk = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 9f, Color = PalmColor, StrokeCap = SKStrokeCap.Round };
        // Curved trunk drawn as a quadratic bezier — base, mid (with lean), top.
        float topX = baseX + lean * height;
        float topY = baseY - height;
        float midX = baseX + lean * height * 0.5f - lean * 40f;
        float midY = baseY - height * 0.5f;
        using var pb = new SKPathBuilder();
        pb.MoveTo(baseX, baseY);
        pb.QuadTo(midX, midY, topX, topY);
        using var path = pb.Detach();
        c.DrawPath(path, trunk);

        // Seven drooping fronds fanning out from the top, each a long curve.
        using var frond = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 5.5f, Color = PalmColor, StrokeCap = SKStrokeCap.Round };
        for (int i = 0; i < 7; i++)
        {
            float a = -MathF.PI / 2f + (i - 3) * 0.45f;
            float len = height * 0.40f;
            float ex = topX + MathF.Cos(a) * len;
            float ey = topY + MathF.Sin(a) * len + len * 0.35f; // droop
            float mx = topX + MathF.Cos(a) * len * 0.5f;
            float my = topY + MathF.Sin(a) * len * 0.5f - 8f;
            using var pf = new SKPathBuilder();
            pf.MoveTo(topX, topY);
            pf.QuadTo(mx, my, ex, ey);
            using var fp = pf.Detach();
            c.DrawPath(fp, frond);
        }
    }
}
