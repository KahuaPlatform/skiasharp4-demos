using SkiaSharp;
using UnoGallery.Diagnostics;
using UnoGallery.LiveTiles;
using UnoGallery.Models;
using UnoGallery.Shaders;

namespace UnoGallery.Effects;

/// <summary>
/// Per-frame render pipeline. Three transition modes:
///
///   - <b>Lerp</b> (default fallback): one composite render with placements
///     linearly interpolated between current and target.
///   - <b>SKSL Dissolve</b> (between non-Detail layouts when enabled): records
///     current and target as separate <see cref="SKPicture"/>s and blends them
///     via <c>Shaders/Dissolve.sksl</c>, a noise-thresholded crossfade.
///   - <b>SKSL Iris</b> (when Detail is involved): same two-picture record,
///     blended via <c>Shaders/Iris.sksl</c> — a circular reveal centred on
///     the focused tile's position in the non-Detail layout.
///
/// Post-pass order is the same regardless of mode: scene → bloom (default
/// path only) → hover glow → vignette → grain → detail overlay → HUD.
/// </summary>
public sealed class EffectsPipeline
{
    readonly BackgroundPass _background = new();
    readonly ReflectionPass _reflection = new();
    readonly BloomPass _bloom = new();
    readonly VignettePass _vignette = new();
    readonly FilmGrainPass _grain = new();

    static readonly SKSamplingOptions HighQuality = new(SKCubicResampler.Mitchell);

    public void Render(
        SKCanvas canvas,
        SKSize size,
        GallerySceneState state,
        ItemPlacement[] current,
        ItemPlacement[]? target)
    {
        var itemsById = new Dictionary<int, GalleryItem>(state.Items.Length);
        foreach (var it in state.Items) itemsById[it.Id] = it;

        var lib = ShaderLibrary.Instance;
        bool inTransition = target is not null;
        bool detailInvolved = inTransition
            && (state.CurrentLayout == LayoutMode.Detail || state.TargetLayout == LayoutMode.Detail);

        SKRuntimeEffect? dissolve = state.Settings.EnableDissolveTransition && !detailInvolved
            ? lib.Dissolve : null;
        SKRuntimeEffect? iris = state.Settings.EnableIrisTransition && detailInvolved
            ? lib.Iris : null;

        using (FrameProfiler.Measure("frame.total"))
        {
            if (inTransition && dissolve is not null)
            {
                using (FrameProfiler.Measure("transition.dissolve"))
                    DrawDissolve(canvas, size, state, current, target!, itemsById, dissolve);
            }
            else if (inTransition && iris is not null)
            {
                using (FrameProfiler.Measure("transition.iris"))
                    DrawIris(canvas, size, state, current, target!, itemsById, iris);
            }
            else
            {
                var placements = inTransition
                    ? LerpAll(current, target!, state.TransitionProgress)
                    : current;
                DrawDefault(canvas, size, state, placements, itemsById);
            }

            DrawDetailOverlay(canvas, size, state);
            DrawLayoutHud(canvas, size, state);
            DrawProfilerHud(canvas, size, state);
        }

        FrameProfiler.EndFrame();
    }

    // ---------- default render path ----------

    void DrawDefault(
        SKCanvas canvas,
        SKSize size,
        GallerySceneState state,
        ItemPlacement[] placements,
        Dictionary<int, GalleryItem> itemsById)
    {
        SKPicture picture;
        using (FrameProfiler.Measure("scene.record"))
            picture = RecordScene(size, state, placements, itemsById);

        SKColorFilter? grade = state.Settings.EnableToneGrade ? ShaderLibrary.Instance.ToneGrade : null;
        SKRuntimeEffect? chroma = state.Settings.EnableChromaShift ? ShaderLibrary.Instance.ChromaShift : null;

        using (FrameProfiler.Measure("scene.compose"))
            DrawScenePicture(canvas, size, state, picture, grade, chroma);

        if (state.Settings.EnableBloom)
            using (FrameProfiler.Measure("bloom"))
                _bloom.Draw(canvas, picture);

        if (state.Settings.EnableHoverGlow
            && state.HoveredItemId is int hoverId
            && ShaderLibrary.Instance.HoverGlow is SKRuntimeEffect glow)
        {
            using (FrameProfiler.Measure("hover-glow"))
                DrawHoverGlow(canvas, placements, itemsById, state.WallClockSeconds, hoverId, glow);
        }

        picture.Dispose();

        if (state.Settings.EnableVignette)
            using (FrameProfiler.Measure("vignette"))
                _vignette.Draw(canvas, size);

        if (state.Settings.EnableGrain)
            using (FrameProfiler.Measure("grain"))
                _grain.Draw(canvas, size, state.WallClockSeconds);
    }

    // ---------- SKSL dissolve path ----------

    void DrawDissolve(
        SKCanvas canvas,
        SKSize size,
        GallerySceneState state,
        ItemPlacement[] current,
        ItemPlacement[] target,
        Dictionary<int, GalleryItem> itemsById,
        SKRuntimeEffect effect)
    {
        using var picA = RecordScene(size, state, current, itemsById);
        using var picB = RecordScene(size, state, target, itemsById);

        var bounds = SKRect.Create(size.Width, size.Height);
        using var shaderA = SKShader.CreatePicture(picA, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, SKMatrix.CreateIdentity(), bounds);
        using var shaderB = SKShader.CreatePicture(picB, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, SKMatrix.CreateIdentity(), bounds);

        float noiseScale = MathF.Max(size.Width, size.Height) * 0.08f;
        using var uniforms = new SKRuntimeEffectUniforms(effect)
        {
            ["iProgress"] = state.TransitionProgress,
            ["iNoiseScale"] = noiseScale,
        };
        using var children = new SKRuntimeEffectChildren(effect)
        {
            ["iSrcA"] = shaderA,
            ["iSrcB"] = shaderB,
        };
        using var shader = effect.ToShader(uniforms, children);

        SKColorFilter? grade = state.Settings.EnableToneGrade ? ShaderLibrary.Instance.ToneGrade : null;
        using var paint = new SKPaint { Shader = shader, ColorFilter = grade };
        canvas.DrawRect(0, 0, size.Width, size.Height, paint);

        if (state.Settings.EnableVignette) _vignette.Draw(canvas, size);
        if (state.Settings.EnableGrain) _grain.Draw(canvas, size, state.WallClockSeconds);
    }

    // ---------- SKSL iris path ----------

    void DrawIris(
        SKCanvas canvas,
        SKSize size,
        GallerySceneState state,
        ItemPlacement[] current,
        ItemPlacement[] target,
        Dictionary<int, GalleryItem> itemsById,
        SKRuntimeEffect effect)
    {
        // Iris is centred on the focused tile's position in the non-Detail layout.
        var nonDetail = state.CurrentLayout == LayoutMode.Detail ? target : current;
        float cx = size.Width * 0.5f, cy = size.Height * 0.5f;
        if (state.FocusedItemId is int fid)
        {
            foreach (var p in nonDetail)
            {
                if (p.ItemId == fid) { cx = p.Center.X; cy = p.Center.Y; break; }
            }
        }
        var centerNorm = new[] { cx / size.Width, cy / size.Height };

        using var picA = RecordScene(size, state, current, itemsById);
        using var picB = RecordScene(size, state, target, itemsById);

        var bounds = SKRect.Create(size.Width, size.Height);
        using var shaderA = SKShader.CreatePicture(picA, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, SKMatrix.CreateIdentity(), bounds);
        using var shaderB = SKShader.CreatePicture(picB, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, SKMatrix.CreateIdentity(), bounds);

        using var uniforms = new SKRuntimeEffectUniforms(effect)
        {
            ["iCenter"] = centerNorm,
            ["iResolution"] = new[] { size.Width, size.Height },
            ["iProgress"] = state.TransitionProgress,
        };
        using var children = new SKRuntimeEffectChildren(effect)
        {
            ["iSrcA"] = shaderA,
            ["iSrcB"] = shaderB,
        };
        using var shader = effect.ToShader(uniforms, children);

        SKColorFilter? grade = state.Settings.EnableToneGrade ? ShaderLibrary.Instance.ToneGrade : null;
        using var paint = new SKPaint { Shader = shader, ColorFilter = grade };
        canvas.DrawRect(0, 0, size.Width, size.Height, paint);

        if (state.Settings.EnableVignette) _vignette.Draw(canvas, size);
        if (state.Settings.EnableGrain) _grain.Draw(canvas, size, state.WallClockSeconds);
    }

    // ---------- scene recording ----------

    SKPicture RecordScene(SKSize size, GallerySceneState state, ItemPlacement[] placements, Dictionary<int, GalleryItem> itemsById)
    {
        var order = SortByZ(placements);
        using var recorder = new SKPictureRecorder();
        var rec = recorder.BeginRecording(SKRect.Create(size.Width, size.Height));
        DrawScene(rec, size, state, placements, order, itemsById);
        return recorder.EndRecording();
    }

    static int[] SortByZ(ItemPlacement[] placements)
    {
        var order = new int[placements.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        for (int i = 1; i < order.Length; i++)
        {
            int k = order[i];
            float kz = placements[k].Z;
            int j = i - 1;
            while (j >= 0 && placements[order[j]].Z > kz) { order[j + 1] = order[j]; j--; }
            order[j + 1] = k;
        }
        return order;
    }

    void DrawScene(
        SKCanvas canvas,
        SKSize size,
        GallerySceneState state,
        ReadOnlySpan<ItemPlacement> placements,
        ReadOnlySpan<int> order,
        Dictionary<int, GalleryItem> itemsById)
    {
        using (FrameProfiler.Measure("background"))
            _background.Draw(canvas, size, state);

        using var tilesRecorder = new SKPictureRecorder();
        var tilesCanvas = tilesRecorder.BeginRecording(SKRect.Create(size.Width, size.Height));
        using (FrameProfiler.Measure("tiles.all"))
        {
            for (int oi = 0; oi < order.Length; oi++)
            {
                var p = placements[order[oi]];
                if (!itemsById.TryGetValue(p.ItemId, out var item)) continue;
                DrawTile(tilesCanvas, p, item, state.WallClockSeconds);
            }
        }
        using var tilesPicture = tilesRecorder.EndRecording();

        using (FrameProfiler.Measure("reflection"))
            _reflection.DrawFromPicture(canvas, size, tilesPicture);

        using (FrameProfiler.Measure("tiles.compose"))
            canvas.DrawPicture(tilesPicture);
    }

    // ---------- scene-picture compositing (default path only) ----------

    static void DrawScenePicture(
        SKCanvas canvas,
        SKSize size,
        GallerySceneState state,
        SKPicture picture,
        SKColorFilter? grade,
        SKRuntimeEffect? chroma)
    {
        if (chroma is not null)
        {
            float baseAmount = 0.0035f;
            float transBoost = state.TargetLayout is not null
                ? 0.030f * (1f - MathF.Abs(state.TransitionProgress - 0.5f) * 2f)
                : 0f;
            float amount = baseAmount + transBoost;

            using var picShader = SKShader.CreatePicture(
                picture,
                SKShaderTileMode.Clamp,
                SKShaderTileMode.Clamp,
                SKMatrix.CreateIdentity(),
                SKRect.Create(size.Width, size.Height));

            using var uniforms = new SKRuntimeEffectUniforms(chroma)
            {
                ["iAmount"] = amount,
                ["iResolution"] = new[] { size.Width, size.Height },
            };
            using var children = new SKRuntimeEffectChildren(chroma)
            {
                ["iSrc"] = picShader,
            };
            using var chromaShader = chroma.ToShader(uniforms, children);

            using var paint = new SKPaint
            {
                Shader = chromaShader,
                ColorFilter = grade,
            };
            canvas.DrawRect(0, 0, size.Width, size.Height, paint);
            return;
        }

        if (grade is not null)
        {
            using var gradePaint = new SKPaint { ColorFilter = grade };
            var identity = SKMatrix.CreateIdentity();
            canvas.DrawPicture(picture, in identity, gradePaint);
            return;
        }

        canvas.DrawPicture(picture);
    }

    // ---------- hover glow (additive on top of tiles) ----------

    static void DrawHoverGlow(
        SKCanvas canvas,
        ItemPlacement[] placements,
        Dictionary<int, GalleryItem> itemsById,
        float time,
        int hoveredItemId,
        SKRuntimeEffect glow)
    {
        ItemPlacement? hovered = null;
        for (int i = 0; i < placements.Length; i++)
        {
            if (placements[i].ItemId == hoveredItemId) { hovered = placements[i]; break; }
        }
        if (hovered is null) return;

        SKColor accent = itemsById.TryGetValue(hoveredItemId, out var item)
            ? item.Palette[Math.Min(2, item.Palette.Length - 1)]
            : new SKColor(255, 200, 100);

        float radius = MathF.Max(hovered.Value.Size.X, hovered.Value.Size.Y) * 1.25f;

        using var uniforms = new SKRuntimeEffectUniforms(glow)
        {
            ["iCenter"] = new[] { hovered.Value.Center.X, hovered.Value.Center.Y },
            ["iRadius"] = radius,
            ["iColor"]  = new[] { accent.Red / 255f, accent.Green / 255f, accent.Blue / 255f },
            ["iTime"]   = time,
        };
        using var shader = glow.ToShader(uniforms);
        using var paint = new SKPaint
        {
            Shader = shader,
            BlendMode = SKBlendMode.Plus,
        };
        // A square big enough to cover the halo — avoid full-screen rect to
        // skip the cost of the shader evaluating on far-away pixels.
        float r = radius * 1.5f;
        canvas.DrawRect(hovered.Value.Center.X - r, hovered.Value.Center.Y - r, r * 2f, r * 2f, paint);
    }

    // ---------- tile draw ----------

    // Cached blur filter — when hovering, ~29 tiles each want the same sigma.
    // Allocate once, reuse across tiles and frames.
    static SKImageFilter? s_blur;
    static float s_blurSigma = -1f;

    static SKImageFilter? GetBlurFilter(float sigma)
    {
        if (sigma <= 0.01f) return null;
        if (s_blur is null || MathF.Abs(s_blurSigma - sigma) > 0.05f)
        {
            s_blur?.Dispose();
            s_blur = SKImageFilter.CreateBlur(sigma, sigma);
            s_blurSigma = sigma;
        }
        return s_blur;
    }

    static void DrawTile(SKCanvas canvas, ItemPlacement p, GalleryItem item, float time)
    {
        var dest = new SKRect(
            p.Center.X - p.Size.X * 0.5f,
            p.Center.Y - p.Size.Y * 0.5f,
            p.Center.X + p.Size.X * 0.5f,
            p.Center.Y + p.Size.Y * 0.5f);

        using (var shadow = new SKPaint
        {
            ImageFilter = SKImageFilter.CreateDropShadowOnly(
                0, 6, 12, 12,
                new SKColor(0, 0, 0, (byte)(120 * p.Opacity))),
        })
        {
            canvas.DrawRect(dest, shadow);
        }

        SKImageFilter? blurFilter = p.Sharpness < 0.999f
            ? GetBlurFilter((1f - p.Sharpness) * 6f)
            : null;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            ImageFilter = blurFilter,
            Color = SKColors.White.WithAlpha((byte)(255 * p.Opacity)),
        };

        canvas.Save();
        if (p.Rotation != 0f)
        {
            canvas.Translate(p.Center.X, p.Center.Y);
            canvas.RotateRadians(p.Rotation);
            canvas.Translate(-p.Center.X, -p.Center.Y);
        }

        // Cull live drawing for tiles that are tiny or near-invisible — Detail
        // mode's perimeter "crumbs" at ~28 px / 18 % opacity aren't worth the
        // simulation cost, and the static snapshot reads the same at that size.
        bool useLive = item.Live is ILiveTile live
            && MathF.Min(p.Size.X, p.Size.Y) >= 30f
            && p.Opacity >= 0.20f;

        if (useLive)
        {
            canvas.Save();
            canvas.ClipRect(dest);

            bool needLayer = blurFilter is not null || p.Opacity < 0.999f;
            using (FrameProfiler.Measure("tile:" + item.Live!.Caption))
            {
                if (needLayer)
                {
                    canvas.SaveLayer(dest, paint);
                    item.Live.Draw(canvas, dest, time);
                    canvas.Restore();
                }
                else
                {
                    item.Live.Draw(canvas, dest, time);
                }
            }

            canvas.Restore();
        }
        else
        {
            canvas.DrawImage(item.Image, dest, HighQuality, paint);
        }

        canvas.Restore();
        // NB: don't dispose blurFilter — it's the cached singleton.
    }

    // ---------- placement lerp (default-path fallback when no SKSL transition) ----------

    static ItemPlacement[] LerpAll(ItemPlacement[] a, ItemPlacement[] b, float t)
    {
        var result = new ItemPlacement[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            result[i] = new ItemPlacement(
                ItemId:    a[i].ItemId,
                Center:    System.Numerics.Vector2.Lerp(a[i].Center, b[i].Center, t),
                Size:      System.Numerics.Vector2.Lerp(a[i].Size, b[i].Size, t),
                Rotation:  a[i].Rotation  + (b[i].Rotation  - a[i].Rotation)  * t,
                Z:         a[i].Z         + (b[i].Z         - a[i].Z)         * t,
                Opacity:   a[i].Opacity   + (b[i].Opacity   - a[i].Opacity)   * t,
                Sharpness: a[i].Sharpness + (b[i].Sharpness - a[i].Sharpness) * t);
        }
        return result;
    }

    // ---------- overlays ----------

    static void DrawDetailOverlay(SKCanvas canvas, SKSize size, GallerySceneState state)
    {
        if (state.CurrentLayout != LayoutMode.Detail || state.TargetLayout is not null) return;
        if (state.FocusedItemId is not int focusId) return;
        GalleryItem? hero = null;
        for (int i = 0; i < state.Items.Length; i++)
            if (state.Items[i].Id == focusId) { hero = state.Items[i]; break; }
        if (hero is null) return;

        float shortEdge = MathF.Min(size.Width, size.Height);
        float heroSize = shortEdge * 0.62f;
        float heroCenterY = size.Height * 0.52f;
        float captionY = heroCenterY + heroSize * 0.5f + 36f;

        using var titleFont = new SKFont { Size = 22 };
        using var subFont = new SKFont { Size = 13 };
        using var titlePaint = new SKPaint { Color = new SKColor(245, 245, 250, 230), IsAntialias = true };
        using var subPaint = new SKPaint { Color = new SKColor(200, 200, 215, 180), IsAntialias = true };

        float titleW = titleFont.MeasureText(hero.Caption);
        canvas.DrawText(hero.Caption, (size.Width - titleW) * 0.5f, captionY, SKTextAlign.Left, titleFont, titlePaint);

        string sub = $"procedural · palette {hero.Palette.Length} swatches";
        float subW = subFont.MeasureText(sub);
        canvas.DrawText(sub, (size.Width - subW) * 0.5f, captionY + 22f, SKTextAlign.Left, subFont, subPaint);

        float swatchSize = 18f;
        float swatchGap = 6f;
        float swatchesW = hero.Palette.Length * swatchSize + (hero.Palette.Length - 1) * swatchGap;
        float swatchX = (size.Width - swatchesW) * 0.5f;
        float swatchY = captionY + 36f;
        for (int i = 0; i < hero.Palette.Length; i++)
        {
            using var fill = new SKPaint { Color = hero.Palette[i], IsAntialias = true };
            using var stroke = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 80),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                IsAntialias = true,
            };
            var rect = new SKRect(swatchX, swatchY, swatchX + swatchSize, swatchY + swatchSize);
            var rrect = new SKRoundRect(rect, 4f);
            canvas.DrawRoundRect(rrect, fill);
            canvas.DrawRoundRect(rrect, stroke);
            swatchX += swatchSize + swatchGap;
        }

        using var hintFont = new SKFont { Size = 12 };
        using var hintPaint = new SKPaint { Color = new SKColor(200, 200, 215, 130), IsAntialias = true };
        const string hint = "Click anywhere or press Esc to return";
        float hintW = hintFont.MeasureText(hint);
        canvas.DrawText(hint, (size.Width - hintW) * 0.5f, size.Height - 24f, SKTextAlign.Left, hintFont, hintPaint);
    }

    static void DrawProfilerHud(SKCanvas canvas, SKSize size, GallerySceneState state)
    {
        if (!state.Settings.ShowProfiler) return;

        // Collect into a list so we can sort/format.
        var rows = new List<(string label, double ms)>();
        double total = 0;
        double frameTotal = 0;
        foreach (var (label, ms) in FrameProfiler.Snapshot())
        {
            if (label == "frame.total") { frameTotal = ms; continue; }
            rows.Add((label, ms));
            // Only outer-level rows contribute to the running total. We treat
            // anything not starting with "tile:" as outer-level; "tile:*" is
            // nested inside "tiles.all" so we don't double-count.
            if (!label.StartsWith("tile:", StringComparison.Ordinal))
                total += ms;
        }

        // Sort by ms descending so the biggest culprits are first.
        rows.Sort((a, b) => b.ms.CompareTo(a.ms));

        using var font = new SKFont { Size = 11f };
        using var labelPaint = new SKPaint { Color = new SKColor(220, 230, 240, 220), IsAntialias = true };
        using var valuePaint = new SKPaint { Color = new SKColor(255, 230, 130, 240), IsAntialias = true };
        using var headerPaint = new SKPaint { Color = new SKColor(255, 255, 255, 230), IsAntialias = true };
        using var bgPaint = new SKPaint { Color = new SKColor(0, 0, 0, 175), IsAntialias = true };

        const float lineH = 14f;
        const float pad = 8f;
        float panelW = 220f;
        // Header + total + each row + spacer
        float panelH = pad + lineH * (rows.Count + 3) + pad;

        // Top-right
        var rect = new SKRect(size.Width - panelW - 12f, 60f, size.Width - 12f, 60f + panelH);
        var rrect = new SKRoundRect(rect, 6f);
        canvas.DrawRoundRect(rrect, bgPaint);

        float x = rect.Left + pad;
        float y = rect.Top + pad + font.Size;

        canvas.DrawText("Frame profiler (ms, EMA)", x, y, SKTextAlign.Left, font, headerPaint);
        y += lineH;

        // Total + fps
        double fps = frameTotal > 0.01 ? 1000.0 / frameTotal : 0;
        string totalRow = $"frame.total      {frameTotal,6:F2}   {fps,5:F0} fps";
        canvas.DrawText(totalRow, x, y, SKTextAlign.Left, font, valuePaint);
        y += lineH;
        y += 2f; // small spacer

        foreach (var (label, ms) in rows)
        {
            // Indent nested per-tile measurements
            bool nested = label.StartsWith("tile:", StringComparison.Ordinal);
            string display = nested ? "  " + label[5..] : label;
            if (display.Length > 22) display = display[..22];
            string row = $"{display,-22}{ms,6:F2}";
            canvas.DrawText(row, x, y, SKTextAlign.Left, font, labelPaint);
            y += lineH;
            if (y > rect.Bottom - pad) break;
        }
    }

    static void DrawLayoutHud(SKCanvas canvas, SKSize size, GallerySceneState state)
    {
        string label = state.TargetLayout is { } target
            ? $"{state.CurrentLayout} -> {target}  {state.TransitionProgress * 100f:F0}%"
            : state.CurrentLayout.ToString();

        using var font = new SKFont { Size = 13 };
        using var fill = new SKPaint { Color = new SKColor(255, 255, 255, 220), IsAntialias = true };
        using var bg = new SKPaint { Color = new SKColor(0, 0, 0, 140), IsAntialias = true };

        float w = font.MeasureText(label) + 20f;
        float h = 24f;
        var rect = new SKRect(16f, 60f, 16f + w, 60f + h);
        var rrect = new SKRoundRect(rect, 6f);
        canvas.DrawRoundRect(rrect, bg);
        canvas.DrawText(label, rect.Left + 10f, rect.Bottom - 7f, SKTextAlign.Left, font, fill);
    }
}
