using System.Runtime.CompilerServices;
using SkiaSharp;
using UnoGallery.Models;
using Windows.Storage;

namespace UnoGallery.Data;

/// <summary>
/// Loads images from a <see cref="StorageFolder"/> via <see cref="SKCodec"/>.
/// Streams items one-by-one through <see cref="LoadAsync"/> so the gallery
/// can render tiles as they decode, rather than blocking on the whole set.
///
/// Tiles are decoded to fit a 512-pixel long edge — enough for the gallery
/// view, the focused Detail view, and the reflection floor without bloating
/// VRAM. A four-swatch palette is sampled from quadrant centres of the decoded
/// bitmap to feed the ambient background's accent uniform.
/// </summary>
public sealed class FolderSource : IImageSource
{
    const int TileMaxEdge = 512;

    static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif",
    };

    readonly StorageFolder _folder;

    public FolderSource(StorageFolder folder) => _folder = folder;

    public async IAsyncEnumerable<GalleryItem> LoadAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        // Uno desktop doesn't implement CreateFileQueryWithOptions; plain
        // GetFilesAsync + in-process filter + sort is portable.
        var all = await _folder.GetFilesAsync();
        var files = all
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f.Name)))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int id = 0;
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var item = await DecodeAsync(file, id, ct).ConfigureAwait(false);
            if (item is not null)
            {
                id++;
                yield return item;
            }
        }
    }

    static async Task<GalleryItem?> DecodeAsync(StorageFile file, int id, CancellationToken ct)
    {
        byte[] bytes;
        try
        {
            using var rawStream = await file.OpenReadAsync();
            using var stream = rawStream.AsStreamForRead();
            using var ms = new MemoryStream(checked((int)Math.Min(stream.Length, int.MaxValue)));
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            bytes = ms.ToArray();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FolderSource] read failed: {file.Path}: {ex.Message}");
            return null;
        }

        return await Task.Run(() => DecodeBytes(bytes, id, Path.GetFileNameWithoutExtension(file.Name)), ct)
                         .ConfigureAwait(false);
    }

    static GalleryItem? DecodeBytes(byte[] bytes, int id, string caption)
    {
        try
        {
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            if (codec is null)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderSource] '{caption}': codec creation returned null");
                return null;
            }

            var origin = codec.EncodedOrigin;

            // Decode at the codec's native resolution. Asking SKCodec.GetPixels
            // for arbitrary smaller dimensions silently fails on JPEG — its
            // codec only supports 1/1, 1/2, 1/4, 1/8 scales — so we decode at
            // native then resize on a canvas (which always works).
            using var native = SKBitmap.Decode(codec);
            if (native is null || native.Width <= 0 || native.Height <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderSource] '{caption}': SKBitmap.Decode returned null");
                return null;
            }

            int srcW = native.Width, srcH = native.Height;
            float scale = Math.Min(1f, (float)TileMaxEdge / Math.Max(srcW, srcH));
            int targetW = Math.Max(1, (int)MathF.Round(srcW * scale));
            int targetH = Math.Max(1, (int)MathF.Round(srcH * scale));

            // Canvas-based downscale — portable across SkiaSharp 3.119 and 4.x
            // (SKBitmap.Resize signature has shifted between versions).
            SKBitmap scaled;
            if (scale < 1f)
            {
                var info = new SKImageInfo(targetW, targetH, SKColorType.Rgba8888, SKAlphaType.Premul);
                scaled = new SKBitmap(info);
                using (var c = new SKCanvas(scaled))
                using (var srcImg = SKImage.FromBitmap(native))
                {
                    var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
                    c.DrawImage(srcImg, new SKRect(0, 0, targetW, targetH), sampling);
                }
            }
            else
            {
                scaled = native;
            }

            try
            {
                var palette = ExtractPalette(scaled);

                // No EXIF rotation needed for the common case.
                if (origin is SKEncodedOrigin.Default or SKEncodedOrigin.TopLeft)
                {
                    return new GalleryItem(id, caption, SKImage.FromBitmap(scaled), palette);
                }

                // Rotated photo: compose into an oriented surface.
                bool swapAxes = origin is
                    SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or
                    SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
                int outW = swapAxes ? targetH : targetW;
                int outH = swapAxes ? targetW : targetH;

                var outInfo = new SKImageInfo(outW, outH, SKColorType.Rgba8888, SKAlphaType.Premul);
                using var surface = SKSurface.Create(outInfo);
                if (surface is null)
                    return new GalleryItem(id, caption, SKImage.FromBitmap(scaled), palette);

                ApplyEncodedOrigin(surface.Canvas, origin, outW, outH);
                using (var srcImg = SKImage.FromBitmap(scaled))
                    surface.Canvas.DrawImage(srcImg, 0, 0);

                return new GalleryItem(id, caption, surface.Snapshot(), palette);
            }
            finally
            {
                if (!ReferenceEquals(scaled, native)) scaled.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FolderSource] '{caption}' decode threw: {ex.Message}");
            return null;
        }
    }

    static void ApplyEncodedOrigin(SKCanvas canvas, SKEncodedOrigin origin, int w, int h)
    {
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(w, 0); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(w, h); canvas.Scale(-1, -1); break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, h); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(w, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(w, h); canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, h); canvas.RotateDegrees(-90); break;
            // TopLeft (default) and anything unexpected: no transform.
        }
    }

    static ImmutableArray<SKColor> ExtractPalette(SKBitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        if (w < 4 || h < 4)
        {
            var c = bmp.GetPixel(w / 2, h / 2);
            return ImmutableArray.Create(c, c, c, c);
        }
        return ImmutableArray.Create(
            bmp.GetPixel(w / 4, h / 4),
            bmp.GetPixel(3 * w / 4, h / 4),
            bmp.GetPixel(w / 4, 3 * h / 4),
            bmp.GetPixel(3 * w / 4, 3 * h / 4));
    }
}
