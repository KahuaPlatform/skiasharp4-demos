using System;
using SkiaSharp;

namespace Launcher.Game;

// Launcher renderer — a card grid styled in the same neon vocabulary as the
// games themselves. Each card shows the game's name in its accent color, a
// Hawaiian-meaning gloss, an "→ <original game>" credit line, a one-line
// tagline, and a Play hint that lights up on hover.
public static class Renderer
{
    static readonly SKColor HudColor    = new(0x33, 0xF8, 0xFF);
    static readonly SKColor DimText     = new(0x88, 0xAA, 0xCC);
    static readonly SKColor CardBg      = new(0x12, 0x08, 0x28, 0xC0);
    static readonly SKColor CardBgHover = new(0x22, 0x10, 0x44, 0xE0);

    const string MarqueeText = "UNO PLATFORM + SKIASHARP NEON DEMO CATALOG  -  CLICK A TILE TO PLAY";

    public static void Render(SKCanvas canvas, LauncherWorld world, float canvasW, float canvasH)
    {
        NeonBackground.Draw(canvas, canvasW, canvasH);

        float scale = MathF.Min(canvasW / world.Width, canvasH / world.Height);
        float ox = (canvasW - world.Width * scale) / 2f;
        float oy = (canvasH - world.Height * scale) / 2f;

        canvas.Save();
        canvas.Translate(ox, oy);
        canvas.Scale(scale);
        DrawWorld(canvas, world);
        canvas.Restore();

        DrawChrome(canvas, world, canvasW, canvasH);
    }

    static void DrawWorld(SKCanvas c, LauncherWorld world)
    {
        DrawCards(c, world);
    }

    static void DrawCards(SKCanvas c, LauncherWorld world)
    {
        var games = GameCatalog.Games;
        int n = games.Length;

        // 4 columns × ceil(N/4) rows on a 1280×720 canvas; tiles below the title.
        const int Cols = 4;
        int rows = (n + Cols - 1) / Cols;

        const float gridTop = 170f;
        const float gridBottom = 620f;
        const float sidePad = 36f;
        const float cardGap = 18f;
        float gridW = world.Width  - sidePad * 2f;
        float gridH = gridBottom - gridTop;

        float cardW = (gridW - cardGap * (Cols - 1)) / Cols;
        float cardH = (gridH - cardGap * (rows - 1)) / rows;

        if (world.CardRects.Length != n) world.CardRects = new SKRect[n];

        // Track the bounding box of cards so we can determine hover index.
        int hovered = -1;
        for (int i = 0; i < n; i++)
        {
            int col = i % Cols;
            int row = i / Cols;
            float x = sidePad + col * (cardW + cardGap);
            float y = gridTop + row * (cardH + cardGap);
            var rect = new SKRect(x, y, x + cardW, y + cardH);
            world.CardRects[i] = rect;
            if (rect.Contains(world.PointerX, world.PointerY)) hovered = i;
        }
        world.HoverIndex = hovered;

        for (int i = 0; i < n; i++)
        {
            DrawCard(c, world, i, world.CardRects[i], i == hovered);
        }
    }

    static void DrawCard(SKCanvas c, LauncherWorld world, int index, SKRect rect, bool hovered)
    {
        var g = GameCatalog.Games[index];

        // Background fill (slightly brighter on hover).
        using (var bg = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = hovered ? CardBgHover : CardBg,
        })
        {
            c.DrawRoundRect(rect, 6f, 6f, bg);
        }

        // Neon border in the game's accent color.
        SKColor border = g.Color;
        NeonPaints.StrokeHalo.StrokeWidth = hovered ? 8f : 5f;
        NeonPaints.StrokeHalo.Color = border.WithAlpha((byte)(hovered ? 0xD0 : 0x90));
        c.DrawRoundRect(rect, 6f, 6f, NeonPaints.StrokeHalo);
        NeonPaints.StrokeSharp.StrokeWidth = hovered ? 2.2f : 1.4f;
        NeonPaints.StrokeSharp.Color = border;
        c.DrawRoundRect(rect, 6f, 6f, NeonPaints.StrokeSharp);
        NeonPaints.StrokeHalo.StrokeWidth  = NeonPaints.DefaultStrokeHaloWidth;
        NeonPaints.StrokeSharp.StrokeWidth = NeonPaints.DefaultStrokeSharpWidth;

        // Title — neon glyph font, accent colored.
        float titleY = rect.Top + 28f;
        float advance = GlyphFont.CharAdvance * 0.55f;
        float titleW = g.Name.Length * advance - GlyphFont.CharGap * 0.55f;
        c.Save();
        c.Translate(rect.MidX - titleW / 2f, titleY);
        c.Scale(0.55f);
        for (int k = 0; k < g.Name.Length; k++)
        {
            if (!GlyphFont.Glyphs.TryGetValue(g.Name[k], out var glyph)) continue;
            c.Save();
            c.Translate(k * GlyphFont.CharAdvance, 0f);
            NeonPaints.MarqueeHalo.Color = g.Color.WithAlpha(0xC0);
            c.DrawPath(glyph, NeonPaints.MarqueeHalo);
            NeonPaints.MarqueeSharp.Color = g.Color;
            c.DrawPath(glyph, NeonPaints.MarqueeSharp);
            c.Restore();
        }
        c.Restore();

        // Hawaiian gloss + original-game credit — pushed well below the title
        // so the neon glow on the title letters doesn't visually merge into it.
        using var glossFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 14);
        IconText.Draw(c, rect.MidX, rect.Top + 105f, SKTextAlign.Center, glossFont, DimText,
            $"\"{g.Gloss}\"", IconText.Icon.Arrow, g.OriginalGame);

        // Description tagline. Wrap to fit the card width; cap at 3 lines so
        // long descriptions don't push into the Play hint.
        using var descFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 14);
        var lines = WrapText(g.Description, (int)((rect.Width - 32f) / 8f));
        for (int li = 0; li < lines.Length && li < 3; li++)
        {
            HudText.Draw(c, lines[li], rect.MidX, rect.Top + 140f + li * 18f,
                SKTextAlign.Center, descFont, HudColor);
        }

        // Play hint pinned to the bottom — same offset regardless of how many
        // description lines wrapped, so the cards line up visually.
        using var playFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 18);
        SKColor playColor = hovered ? g.Color : DimText;
        if (hovered)
            IconText.Draw(c, rect.MidX, rect.Bottom - 14f, SKTextAlign.Center, playFont, playColor,
                IconText.Icon.Triangle, "PLAY");
        else
            HudText.Draw(c, "PLAY", rect.MidX, rect.Bottom - 14f, SKTextAlign.Center, playFont, playColor);
    }

    // Crude word-wrap that splits a string on spaces into lines no longer than
    // approximately maxChars characters each.
    static string[] WrapText(string text, int maxChars)
    {
        if (text.Length <= maxChars) return new[] { text };
        var words = text.Split(' ');
        var lines = new System.Collections.Generic.List<string>();
        var cur = "";
        foreach (var w in words)
        {
            if (cur.Length == 0) cur = w;
            else if (cur.Length + 1 + w.Length <= maxChars) cur += " " + w;
            else { lines.Add(cur); cur = w; }
        }
        if (cur.Length > 0) lines.Add(cur);
        return lines.ToArray();
    }

    static void DrawChrome(SKCanvas c, LauncherWorld world, float cw, float ch)
    {
        // Big rainbow title at the top.
        float titleY = ch * 0.04f;
        c.Save();
        c.Scale(0.85f);
        Marquee.DrawRainbowTitle(c, "UNO SKIA DEMOS", cw / 0.85f, titleY / 0.85f);
        c.Restore();

        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
        HudText.Draw(c, "A NEON ARCADE CATALOG  -  HAWAIIAN-NAMED HOMAGES TO CLASSIC ARCADE GAMES",
            cw / 2f, ch * 0.18f, SKTextAlign.Center, smallFont, HudColor);

        // Marquee at the very bottom — pushed to 0.97 of canvas height so it
        // sits below the card grid rather than overlapping the bottom row.
        Marquee.Draw(c, MarqueeText, cw, ch, baselineFraction: 0.97f);

        // Hover tooltip: when over a card, draw an arrow/glow under the title
        // band so the user can see something happened on mouseover.
        if (world.HoverIndex >= 0 && world.HoverIndex < GameCatalog.Games.Length)
        {
            var g = GameCatalog.Games[world.HoverIndex];
            IconText.Draw(c, cw / 2f, ch - 80f, SKTextAlign.Center, smallFont, g.Color,
                IconText.Icon.Chevron, g.Name, IconText.Icon.EmDash, g.OriginalGame);
        }
    }
}
