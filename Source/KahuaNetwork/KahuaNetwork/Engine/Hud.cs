using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace KahuaNetwork.Engine;

internal sealed class Hud
{
    public SceneRenderer Scene { get; }
    public AIInsightFeed Insights { get; } = new();
    private double _time;
    private float _fps;
    private float _fpsSmooth;
    private readonly Queue<double> _frameStamps = new();

    public List<HudButton> Buttons { get; } = new();

    public Hud(SceneRenderer scene)
    {
        Scene = scene;
        // Buttons positioned at runtime in Render
        Buttons.Add(new HudButton("NETWORK VIEW", HudButtonAction.GlobalView));
        Buttons.Add(new HudButton("REGENERATE NETWORK", HudButtonAction.Regenerate));
        Buttons.Add(new HudButton("TOGGLE GRID", HudButtonAction.ToggleGrid));
        Buttons.Add(new HudButton("AI: AUTO-ROUTE", HudButtonAction.Mitigate));
    }

    public void Update(double dt)
    {
        _time += dt;
        Insights.Update(dt, Scene.City);
        _frameStamps.Enqueue(_time);
        while (_frameStamps.Count > 60) _frameStamps.Dequeue();
        if (_frameStamps.Count > 1)
        {
            var span = _frameStamps.Last() - _frameStamps.First();
            _fps = (float)((_frameStamps.Count - 1) / Math.Max(0.001, span));
            _fpsSmooth = _fpsSmooth * 0.9f + _fps * 0.1f;
        }
    }

    public void Render(SKCanvas canvas)
    {
        float w = Scene.ViewportWidth;
        float h = Scene.ViewportHeight;

        DrawTitle(canvas, w, h);
        DrawStatsPanel(canvas, w, h);
        DrawInsightPanel(canvas, w, h);
        DrawSelectionPanel(canvas, w, h);
        DrawButtons(canvas, w, h);
        DrawCorners(canvas, w, h);
    }

    private void DrawTitle(SKCanvas canvas, float w, float h)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = Theme.TextPrimary };
        using var titleFont = new SKFont(SKTypeface.Default, 26f);
        using var subFont = new SKFont(SKTypeface.Default, 12f);
        canvas.DrawText("THE KAHUA NETWORK", 28, 50, SKTextAlign.Left, titleFont, paint);
        paint.Color = Theme.TextSecondary;
        canvas.DrawText($"LIVING DIGITAL TWIN  ·  {Scene.City.Buildings.Count} ORGANIZATIONS  ·  ENTER ONCE · CONNECT EVERYWHERE",
            28, 70, SKTextAlign.Left, subFont, paint);
    }

    private void DrawStatsPanel(SKCanvas canvas, float w, float h)
    {
        var rect = new SKRect(w - 320, 28, w - 28, 228);
        DrawGlassPanel(canvas, rect);

        using var labelPaint = new SKPaint { IsAntialias = true, Color = Theme.TextSecondary };
        using var valuePaint = new SKPaint { IsAntialias = true, Color = Theme.Cyan };
        using var labelFont = new SKFont(SKTypeface.Default, 10f);
        using var valueFont = new SKFont(SKTypeface.Default, 22f) { Embolden = true };
        using var miniFont = new SKFont(SKTypeface.Default, 9f);

        canvas.DrawText("PERFORMANCE", rect.Left + 14, rect.Top + 22, SKTextAlign.Left, labelFont, labelPaint);
        canvas.DrawText($"{_fpsSmooth:0} FPS", rect.Left + 14, rect.Top + 50, SKTextAlign.Left, valueFont, valuePaint);

        double avgRisk = Scene.City.Buildings.Count == 0 ? 0 : Scene.City.Buildings.Average(b => b.Risk);
        double avgComp = Scene.City.Buildings.Count == 0 ? 0 : Scene.City.Buildings.Average(b => b.Completion);
        labelPaint.Color = Theme.TextSecondary;
        canvas.DrawText("APPROVAL BACKLOG", rect.Left + 14, rect.Top + 82, SKTextAlign.Left, labelFont, labelPaint);

        // Risk bar
        var barRect = new SKRect(rect.Left + 14, rect.Top + 90, rect.Right - 14, rect.Top + 96);
        using var barBg = new SKPaint { Color = Theme.GridFar };
        canvas.DrawRoundRect(barRect, 3, 3, barBg);
        var fillBar = new SKRect(barRect.Left, barRect.Top, barRect.Left + barRect.Width * (float)avgRisk, barRect.Bottom);
        using var barFill = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(barRect.Left, 0), new SKPoint(barRect.Right, 0),
                new[] { Theme.Lime, Theme.Amber, Theme.Red },
                new[] { 0f, 0.6f, 1f },
                SKShaderTileMode.Clamp),
            BlendMode = SKBlendMode.SrcOver,
        };
        canvas.DrawRoundRect(fillBar, 3, 3, barFill);

        canvas.DrawText("PROJECTS ON-TRACK", rect.Left + 14, rect.Top + 120, SKTextAlign.Left, labelFont, labelPaint);
        var compRect = new SKRect(rect.Left + 14, rect.Top + 128, rect.Right - 14, rect.Top + 134);
        canvas.DrawRoundRect(compRect, 3, 3, barBg);
        var compFill = new SKRect(compRect.Left, compRect.Top, compRect.Left + compRect.Width * (float)avgComp, compRect.Bottom);
        using var compPaint = new SKPaint
        {
            Color = Theme.Cyan,
        };
        canvas.DrawRoundRect(compFill, 3, 3, compPaint);

        canvas.DrawText("DOCS IN FLIGHT", rect.Left + 14, rect.Top + 156, SKTextAlign.Left, labelFont, labelPaint);
        valuePaint.Color = Theme.Magenta;
        using var valFontSm = new SKFont(SKTypeface.Default, 16f) { Embolden = true };
        canvas.DrawText($"{Scene.Particles.Count:N0}", rect.Right - 14, rect.Top + 156, SKTextAlign.Right, valFontSm, valuePaint);

        canvas.DrawText("ACTIVE EXCHANGES", rect.Left + 14, rect.Top + 178, SKTextAlign.Left, labelFont, labelPaint);
        valuePaint.Color = Theme.Lime;
        canvas.DrawText($"{Scene.City.DataStreams.Count}", rect.Right - 14, rect.Top + 178, SKTextAlign.Right, valFontSm, valuePaint);
    }

    private void DrawInsightPanel(SKCanvas canvas, float w, float h)
    {
        var rect = new SKRect(28, h - 220, 460, h - 28);
        DrawGlassPanel(canvas, rect);

        using var titleFont = new SKFont(SKTypeface.Default, 12f) { Embolden = true };
        using var bodyFont = new SKFont(SKTypeface.Default, 11f);
        using var paint = new SKPaint { IsAntialias = true, Color = Theme.Cyan };
        canvas.DrawText("⌬  NETWORK ACTIVITY  ·  LIVE", rect.Left + 14, rect.Top + 22, SKTextAlign.Left, titleFont, paint);

        float y = rect.Top + 44;
        foreach (var insight in Insights.Recent.Reverse())
        {
            var dotColor = insight.Kind switch
            {
                InsightKind.Risk => Theme.Red,
                InsightKind.Win => Theme.Lime,
                _ => Theme.Cyan,
            };
            using var dotPaint = new SKPaint { IsAntialias = true, Color = dotColor };
            canvas.DrawCircle(rect.Left + 18, y - 4, 3.5f, dotPaint);
            paint.Color = Theme.TextPrimary;
            // Word-wrap manually within available width
            float maxWidth = rect.Width - 36;
            foreach (var line in WrapText(insight.Text, bodyFont, maxWidth))
            {
                canvas.DrawText(line, rect.Left + 30, y, SKTextAlign.Left, bodyFont, paint);
                y += 14;
            }
            y += 6;
            if (y > rect.Bottom - 14) break;
        }
    }

    private void DrawSelectionPanel(SKCanvas canvas, float w, float h)
    {
        var b = Scene.SelectedBuilding ?? Scene.HoveredBuilding;
        if (b == null) return;
        float panelW = 300;
        var rect = new SKRect(w - panelW - 28, h - 280, w - 28, h - 28);
        DrawGlassPanel(canvas, rect);

        using var paint = new SKPaint { IsAntialias = true, Color = Theme.TextPrimary };
        using var titleFont = new SKFont(SKTypeface.Default, 16f) { Embolden = true };
        using var labelFont = new SKFont(SKTypeface.Default, 10f);
        using var bodyFont = new SKFont(SKTypeface.Default, 12f);

        canvas.DrawText(b.Name, rect.Left + 14, rect.Top + 26, SKTextAlign.Left, titleFont, paint);

        // Role chip
        var chipColor = b.Role.Color();
        var chipText = b.Role.Tag();
        using var chipFont = new SKFont(SKTypeface.Default, 10f) { Embolden = true };
        float chipW = chipFont.MeasureText(chipText) + 14;
        var chipRect = new SKRect(rect.Left + 14, rect.Top + 38, rect.Left + 14 + chipW, rect.Top + 56);
        using var chipFill = new SKPaint { IsAntialias = true, Color = chipColor.WithAlpha(60) };
        canvas.DrawRoundRect(chipRect, 4, 4, chipFill);
        using var chipStroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = chipColor,
            BlendMode = SKBlendMode.Plus,
        };
        canvas.DrawRoundRect(chipRect, 4, 4, chipStroke);
        using var chipPaint = new SKPaint { IsAntialias = true, Color = chipColor };
        canvas.DrawText(chipText, chipRect.Left + 7, chipRect.MidY + 4, SKTextAlign.Left, chipFont, chipPaint);

        paint.Color = Theme.TextSecondary;
        canvas.DrawText($"{b.Role.Display().ToUpperInvariant()}   ·  {b.ActiveProjects} ACTIVE PROJECTS",
            chipRect.Right + 10, rect.Top + 52, SKTextAlign.Left, labelFont, paint);

        // Backlog bar (formerly risk)
        canvas.DrawText("BACKLOG", rect.Left + 14, rect.Top + 76, SKTextAlign.Left, labelFont, paint);
        var rRect = new SKRect(rect.Left + 60, rect.Top + 68, rect.Right - 14, rect.Top + 78);
        using var bg = new SKPaint { Color = Theme.GridFar };
        canvas.DrawRoundRect(rRect, 4, 4, bg);
        var fRect = new SKRect(rRect.Left, rRect.Top, rRect.Left + rRect.Width * (float)b.Risk, rRect.Bottom);
        using var fill = new SKPaint { Color = Theme.RiskColor(b.Risk) };
        canvas.DrawRoundRect(fRect, 4, 4, fill);

        canvas.DrawText("ON-TRACK", rect.Left + 14, rect.Top + 102, SKTextAlign.Left, labelFont, paint);
        var cRect = new SKRect(rect.Left + 78, rect.Top + 94, rect.Right - 14, rect.Top + 104);
        canvas.DrawRoundRect(cRect, 4, 4, bg);
        var cFill = new SKRect(cRect.Left, cRect.Top, cRect.Left + cRect.Width * (float)b.Completion, cRect.Bottom);
        using var cPaint = new SKPaint { Color = Theme.Cyan };
        canvas.DrawRoundRect(cFill, 4, 4, cPaint);

        // Document throughput sparkline
        canvas.DrawText($"DOC THROUGHPUT  ·  {b.DocsThisWeek}/wk  ·  {b.PendingApprovals} PENDING",
            rect.Left + 14, rect.Top + 128, SKTextAlign.Left, labelFont, paint);
        float sparkX = rect.Left + 14;
        float sparkY = rect.Top + 145;
        float sparkW = rect.Width - 28;
        float sparkH = 50;
        using var sparkBg = new SKPaint { Color = new SKColor(0, 0, 0, 80) };
        canvas.DrawRoundRect(new SKRect(sparkX, sparkY, sparkX + sparkW, sparkY + sparkH), 4, 4, sparkBg);
        using var sparkPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.4f,
            Color = b.BaseColor,
            BlendMode = SKBlendMode.Plus,
        };
        const int samples = 60;
        using var path = new SKPath();
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)(samples - 1);
            float lookback = (float)(_time - (1f - t) * 6);
            float v = (float)(0.5 + 0.5 * Math.Sin((lookback + b.TelemetryPhase) * b.TelemetrySpeed * 2.0))
                      * (1 - (float)b.Risk * 0.2f)
                      + (float)Math.Sin(lookback * 7 + b.TelemetryPhase) * 0.08f;
            float x = sparkX + t * sparkW;
            float y = sparkY + sparkH - 6 - v * (sparkH - 12);
            if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
        }
        canvas.DrawPath(path, sparkPaint);

        // Backlog hint
        if (b.Risk > 0.55)
        {
            paint.Color = Theme.Red;
            using var hintFont = new SKFont(SKTypeface.Default, 11f) { Embolden = true };
            canvas.DrawText("⚠ APPROVAL BACKLOG ELEVATED",
                rect.Left + 14, rect.Top + 218, SKTextAlign.Left, hintFont, paint);
            paint.Color = Theme.TextSecondary;
            canvas.DrawText("Press [AI: AUTO-ROUTE]", rect.Left + 14, rect.Top + 234, SKTextAlign.Left, bodyFont, paint);
        }
    }

    private void DrawButtons(SKCanvas canvas, float w, float h)
    {
        float bw = 170;
        float bh = 36;
        float gap = 12;
        float startX = w / 2f - (Buttons.Count * (bw + gap) - gap) / 2f;
        float y = h - bh - 16;
        for (int i = 0; i < Buttons.Count; i++)
        {
            var b = Buttons[i];
            b.Bounds = new SKRect(startX + i * (bw + gap), y, startX + i * (bw + gap) + bw, y + bh);
            DrawButton(canvas, b);
        }
    }

    private void DrawButton(SKCanvas canvas, HudButton b)
    {
        using var fill = new SKPaint
        {
            IsAntialias = true,
            Color = b.Hovered ? Theme.Cyan.WithAlpha(60) : Theme.PanelFill,
        };
        canvas.DrawRoundRect(b.Bounds, 6, 6, fill);
        using var stroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.2f,
            Color = b.Hovered ? Theme.Cyan : Theme.PanelStroke,
            BlendMode = SKBlendMode.Plus,
        };
        canvas.DrawRoundRect(b.Bounds, 6, 6, stroke);

        using var font = new SKFont(SKTypeface.Default, 12f) { Embolden = true };
        using var paint = new SKPaint { IsAntialias = true, Color = b.Hovered ? Theme.TextPrimary : Theme.Cyan };
        float tw = font.MeasureText(b.Label);
        canvas.DrawText(b.Label,
            b.Bounds.MidX - tw / 2f,
            b.Bounds.MidY + 4,
            SKTextAlign.Left, font, paint);
    }

    private void DrawCorners(SKCanvas canvas, float w, float h)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.4f,
            Color = Theme.Cyan.WithAlpha(140),
            BlendMode = SKBlendMode.Plus,
        };
        const float len = 18;
        // top-left
        canvas.DrawLine(8, 8, 8 + len, 8, paint);
        canvas.DrawLine(8, 8, 8, 8 + len, paint);
        // top-right
        canvas.DrawLine(w - 8, 8, w - 8 - len, 8, paint);
        canvas.DrawLine(w - 8, 8, w - 8, 8 + len, paint);
        // bottom-left
        canvas.DrawLine(8, h - 8, 8 + len, h - 8, paint);
        canvas.DrawLine(8, h - 8, 8, h - 8 - len, paint);
        // bottom-right
        canvas.DrawLine(w - 8, h - 8, w - 8 - len, h - 8, paint);
        canvas.DrawLine(w - 8, h - 8, w - 8, h - 8 - len, paint);
    }

    private void DrawGlassPanel(SKCanvas canvas, SKRect rect)
    {
        // Backdrop blur is expensive; emulate glassmorphism with translucent fill + stroke + subtle inner highlight
        using var fill = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top),
                new SKPoint(rect.Left, rect.Bottom),
                new[]
                {
                    new SKColor(0x1F, 0x2C, 0x42, 200),
                    new SKColor(0x0C, 0x14, 0x24, 180),
                },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRoundRect(rect, 10, 10, fill);

        using var stroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = Theme.Cyan.WithAlpha(60),
            BlendMode = SKBlendMode.Plus,
        };
        canvas.DrawRoundRect(rect, 10, 10, stroke);

        // Top highlight stripe
        using var hi = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top),
                new SKPoint(rect.Left, rect.Top + 16),
                new[] { Theme.Cyan.WithAlpha(40), Theme.Cyan.WithAlpha(0) },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp),
            BlendMode = SKBlendMode.Plus,
        };
        canvas.DrawRoundRect(new SKRect(rect.Left, rect.Top, rect.Right, rect.Top + 16), 10, 10, hi);
    }

    private static IEnumerable<string> WrapText(string text, SKFont font, float maxWidth)
    {
        var words = text.Split(' ');
        var line = "";
        foreach (var w in words)
        {
            var test = line.Length == 0 ? w : line + " " + w;
            if (font.MeasureText(test) <= maxWidth)
            {
                line = test;
            }
            else
            {
                if (line.Length > 0) yield return line;
                line = w;
            }
        }
        if (line.Length > 0) yield return line;
    }

    public HudButton? HitTestButton(SKPoint p)
    {
        foreach (var b in Buttons)
        {
            if (b.Bounds.Contains(p)) return b;
        }
        return null;
    }
}

internal enum HudButtonAction { GlobalView, Regenerate, ToggleGrid, Mitigate }

internal sealed class HudButton
{
    public string Label { get; }
    public HudButtonAction Action { get; }
    public SKRect Bounds { get; set; }
    public bool Hovered { get; set; }

    public HudButton(string label, HudButtonAction action)
    {
        Label = label;
        Action = action;
    }
}
