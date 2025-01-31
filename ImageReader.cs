using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;

namespace Tractus.Ndi.Chaos;

public class ImageReader
{
    public static unsafe void LoadImageFileWithoutAlpha(
        string fileName,
        out nint uyvyData,
        out int width, out int height)
    {
        using var stream = File.OpenRead(fileName);
        using var rgbaImage = Image.Load<Rgba32>(stream);

        var fontUYVYData = Marshal.AllocHGlobal(rgbaImage.Width * rgbaImage.Height * 2);
        var fontUYVYDataPtr = (byte*)fontUYVYData.ToPointer();

        ColorSpaceConverter.ConvertRgba32ToYuv422Scalar2(rgbaImage, fontUYVYDataPtr, null);

        uyvyData = fontUYVYData;
        width = rgbaImage.Width;
        height = rgbaImage.Height;
    }

    public static unsafe void LoadImageFileWithAlpha(
        string fileName,
        out nint uyvyData, out nint alphaData,
        out int width, out int height)
    {
        using var stream = File.OpenRead(fileName);

        Configuration.Default.PreferContiguousImageBuffers = true;
        //var config = Configuration.Default.Clone();
        //config.PreferContiguousImageBuffers = true;


        using var originalFile = Image.Load<Rgba32>(stream);
        var toConvert = originalFile;
        //using var toConvert = //originalFile.CloneAs<Rgba32>(config);

        var fontUYVYData = Marshal.AllocHGlobal(toConvert.Width * toConvert.Height * 2);
        var fontUYVYDataPtr = (byte*)fontUYVYData.ToPointer();

        var fontAlphaData = Marshal.AllocHGlobal(toConvert.Width * toConvert.Height);
        var fontAlphaDataPtr = (byte*)fontAlphaData.ToPointer();

        ColorSpaceConverter.ConvertRgba32ToYuv422(toConvert, fontUYVYDataPtr, fontAlphaDataPtr);

        uyvyData = fontUYVYData;
        alphaData = fontAlphaData;
        width = toConvert.Width;
        height = toConvert.Height;

    }
}
