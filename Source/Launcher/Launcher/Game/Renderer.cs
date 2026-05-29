using System;
using SkiaSharp;

namespace Launcher.Game;

// Launcher renderer — a card grid styled either in the neon arcade vocabulary
// (default) or as a Hawaiian Bob Ross painting (toggle with T). Cards always
// show the game's name, a Hawaiian-meaning gloss, an arrow to the original
// game, a one-line tagline, and a Play hint that lights up on hover; the
// per-theme branches paint the framing and text differently.
public static class Renderer
{
    // Neon theme palette.
    static readonly SKColor HudColor    = new(0x33, 0xF8, 0xFF);
    static readonly SKColor DimText     = new(0x88, 0xAA, 0xCC);
    static readonly SKColor CardBg      = new(0x12, 0x08, 0x28, 0xC0);
    static readonly SKColor CardBgHover = new(0x22, 0x10, 0x44, 0xE0);

    // Bob Ross theme palette — cream parchment cards, warm sunset accents,
    // dark espresso brown body text. Card alphas kept low so the painted
    // sunset reads through — they should feel like postcards laid on the
    // canvas, not opaque tiles bolted on top.
    static readonly SKColor RossCardBg      = new(0xFA, 0xF0, 0xDA, 0x88);
    static readonly SKColor RossCardBgHover = new(0xFF, 0xF6, 0xE0, 0xCC);
    static readonly SKColor RossCardFrame   = new(0x4A, 0x32, 0x1E);
    static readonly SKColor RossBodyText    = new(0x3A, 0x28, 0x1C);
    static readonly SKColor RossDimText     = new(0x6E, 0x52, 0x3C);
    static readonly SKColor RossSubtitle    = new(0xFF, 0xEE, 0xC4);
    static readonly SKColor RossTitle       = new(0xFF, 0xF6, 0xD2);

    const string MarqueeText      = "UNO PLATFORM + SKIASHARP NEON DEMO CATALOG  -  CLICK A TILE TO PLAY";
    const string RossMarqueeText  = "A HAPPY LITTLE CATALOG OF HAWAIIAN NEON DEMOS  -  CLICK A TILE TO PLAY";

    public static void Render(SKCanvas canvas, LauncherWorld world, float canvasW, float canvasH)
    {
        if (world.Theme == LauncherTheme.BobRoss)
            BobRossBackground.Draw(canvas, canvasW, canvasH);
        else
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
        if (world.Theme == LauncherTheme.BobRoss) DrawCardBobRoss(c, index, rect, hovered);
        else                                       DrawCardNeon   (c, index, rect, hovered);
    }

    static void DrawCardNeon(SKCanvas c, int index, SKRect rect, bool hovered)
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
        DrawCardTitle(c, rect, g, accent: g.Color, halo: true);

        // Hawaiian gloss + original-game credit.
        using var glossFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 14);
        IconText.Draw(c, rect.MidX, rect.Top + 105f, SKTextAlign.Center, glossFont, DimText,
            $"\"{g.Gloss}\"", IconText.Icon.Arrow, g.OriginalGame);

        // Description tagline (wrapped + 3-line cap).
        using var descFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 14);
        var lines = WrapText(g.Description, (int)((rect.Width - 32f) / 8f));
        for (int li = 0; li < lines.Length && li < 3; li++)
            HudText.Draw(c, lines[li], rect.MidX, rect.Top + 140f + li * 18f, SKTextAlign.Center, descFont, HudColor);

        // Play hint.
        using var playFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 18);
        SKColor playColor = hovered ? g.Color : DimText;
        if (hovered)
            IconText.Draw(c, rect.MidX, rect.Bottom - 14f, SKTextAlign.Center, playFont, playColor,
                IconText.Icon.Triangle, "PLAY");
        else
            HudText.Draw(c, "PLAY", rect.MidX, rect.Bottom - 14f, SKTextAlign.Center, playFont, playColor);
    }

    static void DrawCardBobRoss(SKCanvas c, int index, SKRect rect, bool hovered)
    {
        var g = GameCatalog.Games[index];

        // Cream parchment card with a soft drop shadow so it sits on the
        // painted background instead of floating against it.
        using (var shadow = new SKPaint
        {
            IsAntialias = true,
            Style       = SKPaintStyle.Fill,
            Color       = new SKColor(0x1C, 0x12, 0x08, 0x90),
            MaskFilter  = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10f),
        })
        {
            var shadowRect = new SKRect(rect.Left + 5f, rect.Top + 8f, rect.Right + 5f, rect.Bottom + 8f);
            c.DrawRoundRect(shadowRect, 10f, 10f, shadow);
        }

        // Card fill.
        using (var bg = new SKPaint
        {
            IsAntialias = true,
            Style       = SKPaintStyle.Fill,
            Color       = hovered ? RossCardBgHover : RossCardBg,
        })
        {
            c.DrawRoundRect(rect, 10f, 10f, bg);
        }

        // Wood-frame border — a thick painted stroke in espresso brown, with
        // an inner accent stripe in the game's color so we still get the
        // per-game visual identity.
        using (var frame = new SKPaint
        {
            IsAntialias = true,
            Style       = SKPaintStyle.Stroke,
            StrokeWidth = hovered ? 6f : 4f,
            Color       = RossCardFrame,
        })
        {
            c.DrawRoundRect(rect, 10f, 10f, frame);
        }
        using (var inner = new SKPaint
        {
            IsAntialias = true,
            Style       = SKPaintStyle.Stroke,
            StrokeWidth = 1.6f,
            Color       = g.Color,
        })
        {
            var innerRect = new SKRect(rect.Left + 6f, rect.Top + 6f, rect.Right - 6f, rect.Bottom - 6f);
            c.DrawRoundRect(innerRect, 7f, 7f, inner);
        }

        // Title — painted glyphs, no halo (flat acrylic look), in a deep warm
        // shade derived from the game's accent.
        SKColor titleColor = Darken(g.Color, 0.55f);
        DrawCardTitle(c, rect, g, accent: titleColor, halo: false);

        // Hawaiian gloss + original-game credit in warm brown.
        using var glossFont = new SKFont(SKTypeface.FromFamilyName("Georgia", SKFontStyle.Italic), 14);
        IconText.Draw(c, rect.MidX, rect.Top + 105f, SKTextAlign.Center, glossFont, RossDimText,
            $"\"{g.Gloss}\"", IconText.Icon.Arrow, g.OriginalGame);

        // Description in plain dark brown serif body text.
        using var descFont = new SKFont(SKTypeface.FromFamilyName("Georgia"), 14);
        var lines = WrapText(g.Description, (int)((rect.Width - 32f) / 8f));
        for (int li = 0; li < lines.Length && li < 3; li++)
        {
            using var paint = new SKPaint { IsAntialias = true, Color = RossBodyText };
            c.DrawText(lines[li], rect.MidX, rect.Top + 140f + li * 18f, SKTextAlign.Center, descFont, paint);
        }

        // Play hint — bold serif, accent on hover.
        using var playFont = new SKFont(SKTypeface.FromFamilyName("Georgia", SKFontStyle.Bold), 18);
        SKColor playColor = hovered ? Darken(g.Color, 0.4f) : RossDimText;
        using var playPaint = new SKPaint { IsAntialias = true, Color = playColor };
        if (hovered)
            IconText.Draw(c, rect.MidX, rect.Bottom - 14f, SKTextAlign.Center, playFont, playColor,
                IconText.Icon.Triangle, "PLAY");
        else
            c.DrawText("PLAY", rect.MidX, rect.Bottom - 14f, SKTextAlign.Center, playFont, playPaint);
    }

    static SKColor Darken(SKColor c, float factor) =>
        new SKColor((byte)(c.Red * factor), (byte)(c.Green * factor), (byte)(c.Blue * factor));

    static void DrawCardTitle(SKCanvas c, SKRect rect, GameCatalog.Entry g, SKColor accent, bool halo)
    {
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
            if (halo)
            {
                NeonPaints.MarqueeHalo.Color = accent.WithAlpha(0xC0);
                c.DrawPath(glyph, NeonPaints.MarqueeHalo);
            }
            NeonPaints.MarqueeSharp.Color = accent;
            c.DrawPath(glyph, NeonPaints.MarqueeSharp);
            c.Restore();
        }
        c.Restore();
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
        bool bobRoss = world.Theme == LauncherTheme.BobRoss;
        float titleY = ch * 0.04f;

        // Title — rainbow neon when arcade, painted warm cream when Bob Ross.
        if (bobRoss)
        {
            DrawPaintedTitle(c, "UNO SKIA DEMOS", cw, titleY);
        }
        else
        {
            c.Save();
            c.Scale(0.85f);
            Marquee.DrawRainbowTitle(c, "UNO SKIA DEMOS", cw / 0.85f, titleY / 0.85f);
            c.Restore();
        }

        using var smallFont = new SKFont(SKTypeface.FromFamilyName(bobRoss ? "Georgia" : "Consolas", bobRoss ? SKFontStyle.Italic : SKFontStyle.Normal), 18);
        if (bobRoss)
        {
            using var subPaint = new SKPaint { IsAntialias = true, Color = RossSubtitle };
            c.DrawText("HAPPY LITTLE HAWAIIAN HOMAGES TO CLASSIC ARCADE GAMES",
                cw / 2f, ch * 0.18f, SKTextAlign.Center, smallFont, subPaint);
        }
        else
        {
            HudText.Draw(c, "A NEON ARCADE CATALOG  -  HAWAIIAN-NAMED HOMAGES TO CLASSIC ARCADE GAMES",
                cw / 2f, ch * 0.18f, SKTextAlign.Center, smallFont, HudColor);
        }

        // Theme hint in the top-right corner.
        using var hintFont = new SKFont(SKTypeface.FromFamilyName(bobRoss ? "Georgia" : "Consolas"), 13);
        if (bobRoss)
        {
            using var hintPaint = new SKPaint { IsAntialias = true, Color = RossSubtitle.WithAlpha(0xC0) };
            c.DrawText("T  -  toggle theme", cw - 16f, 24f, SKTextAlign.Right, hintFont, hintPaint);
        }
        else
        {
            HudText.Draw(c, "T  -  toggle theme", cw - 16f, 24f, SKTextAlign.Right, hintFont, DimText);
        }

        // Marquee at the very bottom. Skip in Bob Ross — the painted ocean +
        // palms already occupy that visual band and a scrolling neon strip
        // would fight the painterly mood.
        if (!bobRoss)
            Marquee.Draw(c, MarqueeText, cw, ch, baselineFraction: 0.97f);

        // Hover tooltip — accent-colored under the title band.
        if (world.HoverIndex >= 0 && world.HoverIndex < GameCatalog.Games.Length)
        {
            var g = GameCatalog.Games[world.HoverIndex];
            SKColor tipColor = bobRoss ? Darken(g.Color, 0.55f) : g.Color;
            IconText.Draw(c, cw / 2f, ch - 80f, SKTextAlign.Center, smallFont, tipColor,
                IconText.Icon.Chevron, g.Name, IconText.Icon.EmDash, g.OriginalGame);
        }
    }

    // Bob Ross-style title: drop-shadowed cream lettering using GlyphFont, no
    // rainbow hue cycling — like signing the painted canvas in a single warm
    // pigment instead of neon tubes.
    static void DrawPaintedTitle(SKCanvas c, string title, float cw, float yTop)
    {
        float scale = 0.85f;
        c.Save();
        c.Scale(scale);
        float advance = GlyphFont.CharAdvance;
        float titleW = title.Length * advance - GlyphFont.CharGap;
        float left = (cw / scale - titleW) / 2f;
        float top = yTop / scale;
        using var shadow = new SKPaint
        {
            IsAntialias = true,
            Style       = SKPaintStyle.Stroke,
            StrokeWidth = 6f,
            Color       = new SKColor(0x2A, 0x14, 0x0A, 0xC0),
            MaskFilter  = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f),
        };
        using var sharp  = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = RossTitle };
        for (int i = 0; i < title.Length; i++)
        {
            if (!GlyphFont.Glyphs.TryGetValue(title[i], out var glyph)) continue;
            c.Save();
            c.Translate(left + i * advance, top);
            c.DrawPath(glyph, shadow);
            c.DrawPath(glyph, sharp);
            c.Restore();
        }
        c.Restore();
    }
}
