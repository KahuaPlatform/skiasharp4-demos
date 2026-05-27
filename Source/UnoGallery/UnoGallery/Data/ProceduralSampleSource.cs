using System.Runtime.CompilerServices;
using SkiaSharp;
using UnoGallery.LiveTiles;
using UnoGallery.Models;

namespace UnoGallery.Data;

/// <summary>
/// Generates a deterministic set of 30 visually-distinct "photographs" by drawing
/// directly into <see cref="SKImage"/>s. Six generators × five seeds each gives a
/// gallery that looks like a real curated set without bundling binary assets.
/// </summary>
public sealed class ProceduralSampleSource : IImageSource
{
    const int TileSize = 512;
    const int Count = 30;

    public async IAsyncEnumerable<GalleryItem> LoadAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 0; i < Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = await Task.Run(() => Generate(i), ct).ConfigureAwait(false);
            yield return item;
        }
    }

    static GalleryItem Generate(int seed)
    {
        // Sixteen slots reserved for live tiles, distributed across the 30-item set.
        ILiveTile? live = seed switch
        {
            1  => new PlasmaTile(),
            3  => new GpuMonitorTile(),
            4  => new LissajousTile(),
            6  => new LorenzTile(),
            7  => new FallingSandTile(),
            8  => new FrameTimeTile(),
            9  => new CurlNoiseTile(),
            11 => new ConwayTile(),
            13 => new MandalaTile(),
            14 => new ReactionDiffusionTile(),
            16 => new BoidsTile(),
            18 => new AttractorTile(),
            21 => new WaveformTile(),
            23 => new WireframeTile(),
            24 => new LSystemTile(),
            26 => new ClockTile(),
            _  => null,
        };
        if (live is not null) return CreateLiveItem(seed, live);

        var rng = new Random(seed * 9176 + 17);
        var info = new SKImageInfo(TileSize, TileSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        var palette = BuildPalette(rng);

        canvas.Clear(palette[0]);

        switch (seed % 6)
        {
            case 0: DrawLinearGradient(canvas, palette, rng); break;
            case 1: DrawRadialBurst(canvas, palette, rng); break;
            case 2: DrawJulia(canvas, palette, rng); break;
            case 3: DrawVoronoi(canvas, palette, rng); break;
            case 4: DrawStripes(canvas, palette, rng); break;
            case 5: DrawCurveFlow(canvas, palette, rng); break;
        }

        // Soft inner shadow so tiles read as photographs rather than blocks
        DrawInnerVignette(canvas, palette[0]);

        return new GalleryItem(
            Id: seed,
            Caption: $"Tile {seed:D2}",
            Image: surface.Snapshot(),
            Palette: palette);
    }

    /// <summary>
    /// Build a <see cref="GalleryItem"/> whose Live renderer drives every
    /// frame; the static <see cref="GalleryItem.Image"/> is a one-shot
    /// snapshot at t=0 used by the reflection floor and the warmup grid.
    /// </summary>
    static GalleryItem CreateLiveItem(int seed, ILiveTile tile)
    {
        var info = new SKImageInfo(TileSize, TileSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(tile.Palette.Length > 0 ? tile.Palette[0] : SKColors.Black);
        tile.Draw(surface.Canvas, new SKRect(0, 0, TileSize, TileSize), 0f);
        return new GalleryItem(seed, tile.Caption, surface.Snapshot(), tile.Palette, tile);
    }

    static ImmutableArray<SKColor> BuildPalette(Random rng)
    {
        float baseHue = (float)rng.NextDouble() * 360f;
        float spread = 30f + (float)rng.NextDouble() * 80f;
        return ImmutableArray.Create(
            SKColor.FromHsl(baseHue, 60f + (float)rng.NextDouble() * 30f, 18f + (float)rng.NextDouble() * 10f),
            SKColor.FromHsl((baseHue + spread) % 360f, 70f, 45f),
            SKColor.FromHsl((baseHue + spread * 2f) % 360f, 80f, 65f),
            SKColor.FromHsl((baseHue + 180f) % 360f, 70f, 75f));
    }

    static void DrawLinearGradient(SKCanvas c, ImmutableArray<SKColor> p, Random rng)
    {
        float angle = (float)(rng.NextDouble() * Math.PI * 2);
        var dir = new SKPoint(MathF.Cos(angle) * TileSize, MathF.Sin(angle) * TileSize);
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(TileSize / 2f - dir.X / 2f, TileSize / 2f - dir.Y / 2f),
                new SKPoint(TileSize / 2f + dir.X / 2f, TileSize / 2f + dir.Y / 2f),
                p.ToArray(),
                null,
                SKShaderTileMode.Clamp),
            IsAntialias = true,
        };
        c.DrawRect(0, 0, TileSize, TileSize, paint);
    }

    static void DrawRadialBurst(SKCanvas c, ImmutableArray<SKColor> p, Random rng)
    {
        var center = new SKPoint(TileSize / 2f, TileSize / 2f);
        using (var bg = new SKPaint { Shader = SKShader.CreateRadialGradient(center, TileSize * 0.7f, new[] { p[3], p[1] }, SKShaderTileMode.Clamp) })
            c.DrawRect(0, 0, TileSize, TileSize, bg);

        int rays = 16 + rng.Next(24);
        for (int i = 0; i < rays; i++)
        {
            float a = i * MathF.PI * 2f / rays + (float)rng.NextDouble() * 0.05f;
            using var paint = new SKPaint
            {
                Color = p[2].WithAlpha((byte)(40 + rng.Next(80))),
                StrokeWidth = 1.5f + (float)rng.NextDouble() * 3f,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
            };
            c.DrawLine(center, new SKPoint(center.X + MathF.Cos(a) * TileSize, center.Y + MathF.Sin(a) * TileSize), paint);
        }
    }

    static void DrawJulia(SKCanvas c, ImmutableArray<SKColor> p, Random rng)
    {
        // Lightweight CPU Julia set. 256×256 sampled then upscaled — fast enough.
        const int W = 256;
        float cx = -0.7f + (float)(rng.NextDouble() - 0.5) * 0.6f;
        float cy = 0.27015f + (float)(rng.NextDouble() - 0.5) * 0.4f;
        const int maxIter = 64;
        using var bmp = new SKBitmap(W, W, SKColorType.Rgba8888, SKAlphaType.Premul);
        unsafe
        {
            var pixels = (uint*)bmp.GetPixels();
            for (int y = 0; y < W; y++)
            for (int x = 0; x < W; x++)
            {
                float zx = (x - W / 2f) / (W / 3f);
                float zy = (y - W / 2f) / (W / 3f);
                int iter = 0;
                while (zx * zx + zy * zy < 4f && iter < maxIter)
                {
                    float t = zx * zx - zy * zy + cx;
                    zy = 2f * zx * zy + cy;
                    zx = t;
                    iter++;
                }
                float t01 = iter / (float)maxIter;
                var col = LerpPalette(p, t01);
                pixels[y * W + x] = PackRgba(col);
            }
        }
        using var img = SKImage.FromBitmap(bmp);
        c.DrawImage(img, new SKRect(0, 0, TileSize, TileSize), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
    }

    static void DrawVoronoi(SKCanvas c, ImmutableArray<SKColor> p, Random rng)
    {
        int sites = 18 + rng.Next(18);
        var pts = new SKPoint[sites];
        var cols = new SKColor[sites];
        for (int i = 0; i < sites; i++)
        {
            pts[i] = new SKPoint((float)rng.NextDouble() * TileSize, (float)rng.NextDouble() * TileSize);
            cols[i] = LerpPalette(p, (float)rng.NextDouble());
        }
        const int W = 128;
        using var bmp = new SKBitmap(W, W, SKColorType.Rgba8888, SKAlphaType.Premul);
        unsafe
        {
            var pixels = (uint*)bmp.GetPixels();
            for (int y = 0; y < W; y++)
            for (int x = 0; x < W; x++)
            {
                float fx = x * (TileSize / (float)W);
                float fy = y * (TileSize / (float)W);
                float best = float.MaxValue;
                int bestIdx = 0;
                for (int i = 0; i < sites; i++)
                {
                    float dx = pts[i].X - fx, dy = pts[i].Y - fy;
                    float d = dx * dx + dy * dy;
                    if (d < best) { best = d; bestIdx = i; }
                }
                pixels[y * W + x] = PackRgba(cols[bestIdx]);
            }
        }
        using var img = SKImage.FromBitmap(bmp);
        c.DrawImage(img, new SKRect(0, 0, TileSize, TileSize), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
    }

    static void DrawStripes(SKCanvas c, ImmutableArray<SKColor> p, Random rng)
    {
        int count = 3 + rng.Next(4);
        bool horizontal = rng.NextDouble() < 0.5;
        float band = TileSize / (float)count;
        for (int i = 0; i < count; i++)
        {
            using var paint = new SKPaint { Color = LerpPalette(p, i / (float)(count - 1)), IsAntialias = true };
            if (horizontal) c.DrawRect(0, i * band, TileSize, band, paint);
            else c.DrawRect(i * band, 0, band, TileSize, paint);
        }
        // Soft separator highlight
        using var sep = new SKPaint
        {
            Color = SKColors.White.WithAlpha(40),
            StrokeWidth = 1.5f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
        };
        for (int i = 1; i < count; i++)
        {
            if (horizontal) c.DrawLine(0, i * band, TileSize, i * band, sep);
            else c.DrawLine(i * band, 0, i * band, TileSize, sep);
        }
    }

    static void DrawCurveFlow(SKCanvas c, ImmutableArray<SKColor> p, Random rng)
    {
        int curves = 24 + rng.Next(40);
        for (int i = 0; i < curves; i++)
        {
            var start = new SKPoint((float)rng.NextDouble() * TileSize, (float)rng.NextDouble() * TileSize);

#if SKIA_V4
            // v4 prefers the immutable SKPathBuilder; SKPath's mutable MoveTo/LineTo are obsolete.
            using var builder = new SKPathBuilder();
            builder.MoveTo(start);
            float x = start.X, y = start.Y;
            for (int s = 0; s < 24; s++)
            {
                x += ((float)rng.NextDouble() - 0.5f) * 60f;
                y += ((float)rng.NextDouble() - 0.5f) * 60f;
                builder.LineTo(x, y);
            }
            using var path = builder.Snapshot();
#else
            using var path = new SKPath();
            path.MoveTo(start);
            float x = start.X, y = start.Y;
            for (int s = 0; s < 24; s++)
            {
                x += ((float)rng.NextDouble() - 0.5f) * 60f;
                y += ((float)rng.NextDouble() - 0.5f) * 60f;
                path.LineTo(x, y);
            }
#endif
            using var paint = new SKPaint
            {
                Color = LerpPalette(p, (float)rng.NextDouble()).WithAlpha((byte)(80 + rng.Next(120))),
                StrokeWidth = 1f + (float)rng.NextDouble() * 2.5f,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                IsAntialias = true,
            };
            c.DrawPath(path, paint);
        }
    }

    static void DrawInnerVignette(SKCanvas c, SKColor edge)
    {
        var center = new SKPoint(TileSize / 2f, TileSize / 2f);
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                center,
                TileSize * 0.75f,
                new[] { SKColors.Transparent, edge.WithAlpha(160) },
                new[] { 0.55f, 1f },
                SKShaderTileMode.Clamp),
            BlendMode = SKBlendMode.Multiply,
        };
        c.DrawRect(0, 0, TileSize, TileSize, paint);
    }

    static SKColor LerpPalette(ImmutableArray<SKColor> p, float t)
    {
        t = Math.Clamp(t, 0f, 0.999f);
        float scaled = t * (p.Length - 1);
        int idx = (int)scaled;
        float f = scaled - idx;
        return Lerp(p[idx], p[Math.Min(idx + 1, p.Length - 1)], f);
    }

    static SKColor Lerp(SKColor a, SKColor b, float t) => new(
        (byte)(a.Red + (b.Red - a.Red) * t),
        (byte)(a.Green + (b.Green - a.Green) * t),
        (byte)(a.Blue + (b.Blue - a.Blue) * t),
        (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));

    static uint PackRgba(SKColor c) =>
        ((uint)c.Alpha << 24) | ((uint)c.Blue << 16) | ((uint)c.Green << 8) | c.Red;
}
