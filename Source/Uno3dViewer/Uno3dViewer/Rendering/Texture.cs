using System;
using Silk.NET.OpenGL;
using SkiaSharp;

namespace Uno3dViewer.Rendering;

public sealed class Texture : IDisposable
{
    private readonly GL _gl;
    public uint Handle { get; }
    public int Width { get; }
    public int Height { get; }

    public Texture(GL gl, ReadOnlySpan<byte> rgba, int width, int height)
    {
        _gl = gl;
        Width = width;
        Height = height;
        Handle = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, Handle);
        gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Srgb8Alpha8,
            (uint)width, (uint)height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, rgba);
        gl.GenerateMipmap(TextureTarget.Texture2D);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
        gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public static Texture? FromFile(GL gl, string path)
    {
        using var bmp = SKBitmap.Decode(path);
        return FromBitmap(gl, bmp);
    }

    public static unsafe Texture? FromCompressedBytes(GL gl, byte* data, int length)
    {
        using var skdata = SKData.CreateCopy((IntPtr)data, (ulong)length);
        using var bmp = SKBitmap.Decode(skdata);
        return FromBitmap(gl, bmp);
    }

    public static unsafe Texture FromRawBgra(GL gl, byte* bgra, int width, int height)
    {
        var rgba = new byte[width * height * 4];
        var src = new ReadOnlySpan<byte>(bgra, width * height * 4);
        for (int i = 0; i < width * height; i++)
        {
            rgba[i * 4 + 0] = src[i * 4 + 2];
            rgba[i * 4 + 1] = src[i * 4 + 1];
            rgba[i * 4 + 2] = src[i * 4 + 0];
            rgba[i * 4 + 3] = src[i * 4 + 3];
        }
        return new Texture(gl, rgba, width, height);
    }

    private static Texture? FromBitmap(GL gl, SKBitmap? bmp)
    {
        if (bmp is null || bmp.Width == 0 || bmp.Height == 0) return null;
        int w = bmp.Width, h = bmp.Height;
        int pixelCount = w * h;
        var rgba = new byte[pixelCount * 4];
        var src = bmp.GetPixelSpan();

        switch (bmp.ColorType)
        {
            case SKColorType.Rgba8888:
                src.CopyTo(rgba);
                break;
            case SKColorType.Bgra8888:
                for (int i = 0; i < pixelCount; i++)
                {
                    rgba[i * 4 + 0] = src[i * 4 + 2];
                    rgba[i * 4 + 1] = src[i * 4 + 1];
                    rgba[i * 4 + 2] = src[i * 4 + 0];
                    rgba[i * 4 + 3] = src[i * 4 + 3];
                }
                break;
            default:
                {
                    var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                    using var converted = new SKBitmap(info);
                    if (!bmp.CopyTo(converted)) return null;
                    converted.GetPixelSpan().CopyTo(rgba);
                    break;
                }
        }
        return new Texture(gl, rgba, w, h);
    }

    public static Texture CreateWhite(GL gl)
    {
        Span<byte> pixel = stackalloc byte[] { 255, 255, 255, 255 };
        return new Texture(gl, pixel, 1, 1);
    }

    public void Bind(int unit = 0)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + unit);
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Dispose() => _gl.DeleteTexture(Handle);
}
