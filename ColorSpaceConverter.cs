using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;

namespace Tractus.Ndi.Chaos;
// Taken from Multiview for NDI SWEngine.
public class ColorSpaceConverter
{
    public static unsafe void ConvertRgba32ToYuv422(Image<Rgba32> pngFile,
        byte* dataPtr,
        byte* alphaDataPtr)
    {

        //ConvertRgba32ToYuv422Simd(pngFile, dataPtr, alphaDataPtr);
    }

    // Crafted by ChatGPT, it's very slow - actually slower then
    // our scalar version.
    [Obsolete("Slower than the Scalar version. TODO: Fix me.", error: true)]
    public static unsafe void ConvertRgba32ToYuv422Simd(
    Image<Rgba32> pngFile,
    byte* dataPtr,
    byte* alphaDataPtr)
    {
        var width = pngFile.Width;
        var height = pngFile.Height;

        if (!pngFile.DangerousTryGetSinglePixelMemory(out var pixels))
        {
            throw new Exception("Failed to get pixel memory.");
        }

        using var handle = pixels.Pin();
        byte* buffer = (byte*)handle.Pointer;

        // Precompute coefficients as vectors
        Vector<float> coefY = new Vector<float>(new float[] { 0.299f, 0.587f, 0.114f, 0.0f, 0f, 0f, 0f, 0f });
        Vector<float> coefU = new Vector<float>(new[] { -0.14713f, -0.28886f, 0.436f, 0.0f, 0f, 0f, 0f, 0f });
        Vector<float> coefV = new Vector<float>(new[] { 0.615f, -0.51499f, -0.10001f, 0.0f, 0f, 0f, 0f, 0f });
        Vector<float> offsetUV = new Vector<float>(128f);

        for (int y = 0; y < height; y++)
        {
            int x = 0;

            // Process pixels in SIMD chunks
            while (x <= width - Vector<float>.Count)
            {
                int baseOffset = (y * width * 4) + (x * 4);

                // Load R, G, B values into SIMD vectors
                Vector<float> r = new Vector<float>(new float[]
                {
                buffer[baseOffset + 0], buffer[baseOffset + 4], buffer[baseOffset + 8], buffer[baseOffset + 12],
                buffer[baseOffset + 16], buffer[baseOffset + 20], buffer[baseOffset + 24], buffer[baseOffset + 28]
                });
                Vector<float> g = new Vector<float>(new float[]
                {
                buffer[baseOffset + 1], buffer[baseOffset + 5], buffer[baseOffset + 9], buffer[baseOffset + 13],
                buffer[baseOffset + 17], buffer[baseOffset + 21], buffer[baseOffset + 25], buffer[baseOffset + 29]
                });
                Vector<float> b = new Vector<float>(new float[]
                {
                buffer[baseOffset + 2], buffer[baseOffset + 6], buffer[baseOffset + 10], buffer[baseOffset + 14],
                buffer[baseOffset + 18], buffer[baseOffset + 22], buffer[baseOffset + 26], buffer[baseOffset + 30]
                });

                // Compute Y, U, and V for all 8 pixels
                Vector<float> yVec = (r * coefY[0]) + (g * coefY[1]) + (b * coefY[2]);
                Vector<float> uVec = (r * coefU[0]) + (g * coefU[1]) + (b * coefU[2]) + offsetUV;
                Vector<float> vVec = (r * coefV[0]) + (g * coefV[1]) + (b * coefV[2]) + offsetUV;

                // Write the results in UYVY format
                for (int i = 0; i < Vector<float>.Count; i += 2)
                {
                    int dataIndex = (y * width + x + i) * 2;

                    // Downsample U and V by averaging
                    byte u = (byte)Math.Clamp((uVec[i] + uVec[i + 1]) / 2, 0, 255);
                    byte v = (byte)Math.Clamp((vVec[i] + vVec[i + 1]) / 2, 0, 255);

                    // Write UYVY for two pixels
                    dataPtr[dataIndex + 0] = u;                     // U
                    dataPtr[dataIndex + 1] = (byte)Math.Clamp(yVec[i], 0, 255);   // Y0
                    dataPtr[dataIndex + 2] = v;                     // V
                    dataPtr[dataIndex + 3] = (byte)Math.Clamp(yVec[i + 1], 0, 255); // Y1
                }

                x += Vector<float>.Count;
            }

            // Handle remaining pixels with scalar fallback
            for (; x < width; x += 2)
            {
                int offset = (y * width * 4) + (x * 4);

                var r1 = buffer[offset + 0];
                var g1 = buffer[offset + 1];
                var b1 = buffer[offset + 2];

                var y1 = (byte)Math.Clamp(0.299f * r1 + 0.587f * g1 + 0.114f * b1, 0, 255);
                var u1 = -0.14713f * r1 - 0.28886f * g1 + 0.436f * b1;
                var v1 = 0.615f * r1 - 0.51499f * g1 - 0.10001f * b1;

                var r2 = (x + 1 < width) ? buffer[offset + 4] : 0;
                var g2 = (x + 1 < width) ? buffer[offset + 5] : 0;
                var b2 = (x + 1 < width) ? buffer[offset + 6] : 0;

                var y2 = (byte)Math.Clamp(0.299f * r2 + 0.587f * g2 + 0.114f * b2, 0, 255);
                var u2 = -0.14713f * r2 - 0.28886f * g2 + 0.436f * b2;
                var v2 = 0.615f * r2 - 0.51499f * g2 - 0.10001f * b2;

                byte u = (byte)Math.Clamp((u1 + u2) / 2 + 128, 0, 255);
                byte v = (byte)Math.Clamp((v1 + v2) / 2 + 128, 0, 255);

                int dataIndex = (y * width + x) * 2;
                dataPtr[dataIndex + 0] = u;
                dataPtr[dataIndex + 1] = y1;
                dataPtr[dataIndex + 2] = v;
                dataPtr[dataIndex + 3] = y2;
            }
        }
    }

    public static unsafe void ConvertRgba32ToYuv422Scalar2(
        Image<Rgba32> pngFile,
        byte* dataPtr,
        byte* alphaDataPtr)
    {
        var width = pngFile.Width;
        var height = pngFile.Height;

        if (!pngFile.DangerousTryGetSinglePixelMemory(out var pixels))
        {
            throw new Exception("");
        }

        using var handle = pixels.Pin();
        byte* buffer = (byte*)handle.Pointer;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x += 2)
            {
                var offset = (y * width * 4) + (x * 4);

                //var pixel1 = buffer[x];
                //var pixel2 = x + 1 < width ? buffer[x + 1] : new Rgba32(0, 0, 0, 255); // Handle odd width

                // Convert RGB to YUV for pixel1
                var y1 = (byte)Math.Clamp(0.299f * buffer[offset]
                                         + 0.587f * buffer[offset + 1]
                                         + 0.114f * buffer[offset + 2],
                                         0, 255);

                var u1 = (byte)Math.Clamp(-0.14713f * buffer[offset]
                                         - 0.28886f * buffer[offset + 1]
                                         + 0.436f * buffer[offset + 2]
                                         + 128, 0, 255);

                var v1 = (byte)Math.Clamp(0.615f * buffer[offset]
                                         - 0.51499f * buffer[offset + 1]
                                         - 0.10001f * buffer[offset + 2]
                                         + 128, 0, 255);

                // Convert RGB to YUV for pixel2
                var y2 = (byte)Math.Clamp(0.299f * buffer[offset + 4]
                                         + 0.587f * buffer[offset + 5]
                                         + 0.114f * buffer[offset + 6],
                                         0, 255);

                var u2 = (byte)Math.Clamp(-0.14713f * buffer[offset + 4]
                                         - 0.28886f * buffer[offset + 5]
                                         + 0.436f * buffer[offset + 6]
                                         + 128, 0, 255);

                var v2 = (byte)Math.Clamp(0.615f * buffer[offset + 4]
                                         - 0.51499f * buffer[offset + 5]
                                         - 0.10001f * buffer[offset + 6]
                                         + 128, 0, 255);


                // Average U and V values for the macropixel
                var u = (byte)((u1 + u2) / 2);
                var v = (byte)((v1 + v2) / 2);

                var index = (y * width + x) * 2;
                var alphaIndex = y * width + x;

                // Pack UYVY data
                dataPtr[index] = u;
                dataPtr[index + 1] = y1;
                dataPtr[index + 2] = v;
                dataPtr[index + 3] = y2;

                //if (alphaDataPtr is not null)
                //{
                //    alphaDataPtr[alphaIndex] = pixel1.A;
                //    alphaDataPtr[alphaIndex + 1] = pixel2.A;
                //}
            }
        }
    }

    public static unsafe void ConvertRgba32ToYuv422Scalar(
        Image<Rgba32> pngFile,
        byte* dataPtr,
        byte* alphaDataPtr)
    {
        var width = pngFile.Width;
        var height = pngFile.Height;

        for (var y = 0; y < height; y++)
        {
            var rowMemory = pngFile.DangerousGetPixelRowMemory(y);
            var rowSpan = rowMemory.Span;

            for (var x = 0; x < width; x += 2)
            {
                var pixel1 = rowSpan[x];
                var pixel2 = x + 1 < width ? rowSpan[x + 1] : new Rgba32(0, 0, 0, 255); // Handle odd width

                // Convert RGB to YUV for pixel1
                var y1 = (byte)Math.Clamp(0.299f * pixel1.R
                                         + 0.587f * pixel1.G
                                         + 0.114f * pixel1.B,
                                         0, 255);

                var u1 = (byte)Math.Clamp(-0.14713f * pixel1.R
                                         - 0.28886f * pixel1.G
                                         + 0.436f * pixel1.B
                                         + 128, 0, 255);

                var v1 = (byte)Math.Clamp(0.615f * pixel1.R
                                         - 0.51499f * pixel1.G
                                         - 0.10001f * pixel1.B
                                         + 128, 0, 255);

                // Convert RGB to YUV for pixel2
                var y2 = (byte)Math.Clamp(0.299f * pixel2.R
                                         + 0.587f * pixel2.G
                                         + 0.114f * pixel2.B,
                                         0, 255);

                var u2 = (byte)Math.Clamp(-0.14713f * pixel2.R
                                         - 0.28886f * pixel2.G
                                         + 0.436f * pixel2.B
                                         + 128, 0, 255);

                var v2 = (byte)Math.Clamp(0.615f * pixel2.R
                                         - 0.51499f * pixel2.G
                                         - 0.10001f * pixel2.B
                                         + 128, 0, 255);


                // Average U and V values for the macropixel
                var u = (byte)((u1 + u2) / 2);
                var v = (byte)((v1 + v2) / 2);

                var index = (y * width + x) * 2;
                var alphaIndex = y * width + x;

                // Pack UYVY data
                dataPtr[index] = u;
                dataPtr[index + 1] = y1;
                dataPtr[index + 2] = v;
                dataPtr[index + 3] = y2;

                if (alphaDataPtr is not null)
                {
                    alphaDataPtr[alphaIndex] = pixel1.A;
                    alphaDataPtr[alphaIndex + 1] = pixel2.A;
                }
            }
        }
    }
}
