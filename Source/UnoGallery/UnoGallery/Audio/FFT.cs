using System.Numerics;

namespace UnoGallery.Audio;

/// <summary>
/// Tiny in-place radix-2 Cooley-Tukey FFT. Length must be a power of two.
/// Operates on <see cref="System.Numerics.Complex"/> so callers can window
/// real input by zeroing imaginary parts. Throughput is fine at 1024
/// samples — well under 100 µs on a modern desktop.
/// </summary>
public static class FFT
{
    public static void Forward(Span<Complex> data)
    {
        int n = data.Length;
        if ((n & (n - 1)) != 0)
            throw new ArgumentException("FFT length must be a power of two.", nameof(data));

        // Bit-reversal permutation.
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (data[i], data[j]) = (data[j], data[i]);
        }

        // Iterative Danielson-Lanczos butterfly.
        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2.0 * Math.PI / len;
            var wStep = new Complex(Math.Cos(angle), Math.Sin(angle));

            int half = len >> 1;
            for (int i = 0; i < n; i += len)
            {
                var w = Complex.One;
                for (int k = 0; k < half; k++)
                {
                    var u = data[i + k];
                    var v = data[i + k + half] * w;
                    data[i + k] = u + v;
                    data[i + k + half] = u - v;
                    w *= wStep;
                }
            }
        }
    }
}
