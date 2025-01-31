using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using System.Runtime.CompilerServices;
using NewTek;
using NewTek.NDI;

namespace Tractus.Ndi.Chaos;

internal class Program
{
    private static unsafe void Initialize(
        int width,
        int height,
        int frameRate,
        List<nint> frameList)
    {
        var fontPath = "SpaceMono-Regular.ttf";
        var fontCollection = new FontCollection();
        var fontFamily = fontCollection.Add(fontPath);
        var font = fontFamily.CreateFont((float)height * 0.25f);

        for (var i = 0; i < frameRate; i++)
        {
            using var image = new Image<Rgba32>(width, height);

            var blueValue = (int)(255 * (i / (float)(frameRate - 1)));
            var backgroundColor = new Rgba32(0, 0, (byte)blueValue, 255);
            image.Mutate(ctx => ctx.Fill(backgroundColor));

            var frameText = i.ToString();

            var textOptions = new TextOptions(font)
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var textSize = TextMeasurer.MeasureSize(frameText, textOptions);
            var position = new Vector2((width - textSize.Width) / 2, (height - textSize.Height) / 2);

            image.Mutate(ctx => ctx.DrawText(frameText, font, Color.White, position));

            var yuvFrameMemory = Marshal.AllocHGlobal(width * height * 2);

            ColorSpaceConverter.ConvertRgba32ToYuv422Scalar(
                image,
                (byte*)yuvFrameMemory.ToPointer(),
                (byte*)null);

            frameList.Add(yuvFrameMemory);
        }
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var toReturn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var arg in args)
        {
            // Split on the first '='
            var parts = arg.Split(new[] { '=' }, 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();
                toReturn[key] = value; // overwriting if duplicated
            }
        }

        return toReturn;
    }

    static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();

        var width = 1920;
        var height = 1080;
        var frameRate = 30;
        var frameList = new List<nint>();

        var arguments = ParseArguments(args);
        var allowNdiClockVideo = true;
        var clockJitterLowerBound = 0;
        var clockJitterUpperBound = 0;
        var timecodeMode = TimeCodeMode.Synthesize;

        if (args.Length == 0 || args.Any(x => x == "help"))
        {
            Console.WriteLine(@"
==================================
 Tractus.Ndi.Chaos 2025.1.31.1
==================================
 Usage:
   Tractus.Ndi.Chaos.exe [key=value] [key=value] ...

 Available Parameters:
   width=<int>       Sets the output frame width in pixels. 
                     Default is 1920.

   height=<int>      Sets the output frame height in pixels. 
                     Default is 1080.

   fps=<int>         Sets the frames per second.
                     Default is 30.

   clock=override    Overrides the default NDI clock behavior.
                     If present, disables NDI clock video. This is
                     required in order to use jitter settings (below).

   jlo=<int>         Clock jitter lower bound.
                     Default is 0.

   jhi=<int>         Clock jitter upper bound.
                     Default is the same as jlo unless otherwise specified.

   timecode=<mode>   Sets the timecode mode. Accepted values:
                     - invalid
                     - framecounter
                     - systemclock
                     - random
                     - (default = synthesize if none provided)

   name=<source>     Sets the source name. Default is \""Chaos\""

 Description:
   Tractus.Ndi.Chaos 2025.1.31.1 generates test NDI video frames with 
   customizable dimensions, frame rate, clock-jitter simulation, and 
   timecode behavior. The parameters listed above can be provided in 
   the form 'key=value'.

 Example:
   Tractus.Ndi.Chaos.exe width=1280 height=720 fps=25 jlo=10 jhi=50
                           clock=override timecode=random

");
        }

        if (arguments.TryGetValue("width", out var widthStr) && int.TryParse(widthStr, out var tmpWidth))
        {
            width = tmpWidth;
        }

        if (arguments.TryGetValue("height", out var heightStr) && int.TryParse(heightStr, out var tmpHeight))
        {
            height = tmpHeight;
        }

        if (arguments.TryGetValue("fps", out var frameRateStr) && int.TryParse(frameRateStr, out var tmpFrameRate))
        {
            frameRate = tmpFrameRate;
        }

        if (arguments.TryGetValue("clock", out var clockValue) &&
            clockValue.Equals("override", StringComparison.OrdinalIgnoreCase))
        {
            allowNdiClockVideo = false;
        }

        if (arguments.TryGetValue("jlo", out var jitterLowerStr) &&
            int.TryParse(jitterLowerStr, out var tmpJitterLower))
        {
            clockJitterLowerBound = tmpJitterLower;
        }

        if (arguments.TryGetValue("jhi", out var jitterUpperStr) &&
            int.TryParse(jitterUpperStr, out var tmpJitterUpper))
        {
            clockJitterUpperBound = tmpJitterUpper;
        }
        else
        {
            // If not specified or invalid, we can default it to clockJitterLowerBound if needed
            clockJitterUpperBound = clockJitterLowerBound;
        }

        // timecode
        if (arguments.TryGetValue("timecode", out var timecodeValue))
        {
            switch (timecodeValue.ToLowerInvariant())
            {
                case "invalid":
                    timecodeMode = TimeCodeMode.Invalid;
                    break;
                case "framecounter":
                    timecodeMode = TimeCodeMode.FrameCounter;
                    break;
                case "systemclock":
                    timecodeMode = TimeCodeMode.SystemClock;
                    break;
                case "random":
                    timecodeMode = TimeCodeMode.Random;
                    break;
                default:
                    timecodeMode = TimeCodeMode.Synthesize;
                    break;
            }
        }

        var ndiName = "Chaos";
        if(arguments.TryGetValue("name", out var tempName))
        {
            ndiName = tempName;
        }

        Console.WriteLine($"Creating a sender, {width} x {height} @ {frameRate} fps, named {ndiName}");
        if (!allowNdiClockVideo)
        {
            Console.WriteLine($"\tWARNING: Not clocking audio or video! Adding jitter of {clockJitterLowerBound} to {clockJitterUpperBound} msec.");
        }
        Console.WriteLine($"\tTimecode mode: {timecodeMode}");

        var stallRequested = false;
        var stallRequestTime = 0;

        Initialize(
            width,
            height,
            frameRate,
            frameList);

        var ndiTask = Task.Run(() =>
        {
            var senderSettings = new NDIlib.send_create_t
            {
                clock_video = allowNdiClockVideo,
                clock_audio = false,
                p_ndi_name = UTF.StringToUtf8(ndiName)
            };

            var senderPtr = NDIlib.send_create(ref senderSettings);
            Marshal.FreeHGlobal(senderSettings.p_ndi_name);

            var frames = new NDIlib.video_frame_v2_t[frameRate];

            for(var i = 0; i < frames.Length; i++)
            {
                var videoFrame = new NDIlib.video_frame_v2_t
                {
                    FourCC = NDIlib.FourCC_type_e.FourCC_type_UYVY,
                    frame_format_type = NDIlib.frame_format_type_e.frame_format_type_progressive,
                    frame_rate_N = frameRate,
                    frame_rate_D = 1,
                    line_stride_in_bytes = width * 2,
                    picture_aspect_ratio = (float)width / (float)height,
                    p_data = frameList[i],
                    xres = width,
                    yres = height,
                    timecode = NDIlib.send_timecode_synthesize
                };

                frames[i] = videoFrame;
            }

            var frame = 0;

            var lastLoopRunTime = DateTime.Now;
            var random = new Random();
            var iterations = 0L;

            while (!cts.IsCancellationRequested)
            {
                if (!allowNdiClockVideo)
                {
                    // We should sleep between frames.
                    var targetNextFrameTime = lastLoopRunTime.AddMilliseconds(1000.0f / (float)frameRate);
                    targetNextFrameTime = targetNextFrameTime.AddMilliseconds(-1);

                    if(clockJitterLowerBound != 0 || clockJitterUpperBound != 0)
                    {
                        targetNextFrameTime = targetNextFrameTime.AddMilliseconds(random.Next(clockJitterLowerBound, clockJitterUpperBound));
                    }

                    while(DateTime.Now <= targetNextFrameTime)
                    {

                    }
                }

                var wasteLoopTargetMS = 
                    stallRequestTime > 0
                    ? stallRequestTime
                    : new Random().Next(1, 125);
                var targetTime = DateTime.Now.AddMilliseconds(wasteLoopTargetMS);

                if (stallRequested)
                {
                    Console.WriteLine($"Stalling for {wasteLoopTargetMS} msec...");
                    while (DateTime.Now <= targetTime)
                    {
                    }

                    stallRequested = false;
                    stallRequestTime = 0;
                }

                var videoFrame = frames[frame];

                if (timecodeMode == TimeCodeMode.Invalid)
                {
                    videoFrame.timecode = 1;
                }
                else if (timecodeMode == TimeCodeMode.Synthesize)
                {
                    videoFrame.timecode = NDIlib.send_timecode_synthesize;
                }
                else if (timecodeMode == TimeCodeMode.FrameCounter)
                {
                    videoFrame.timecode = iterations;
                }
                else if(timecodeMode == TimeCodeMode.SystemClock)
                {
                    videoFrame.timecode = DateTime.Now.Ticks;
                }
                else if(timecodeMode == TimeCodeMode.Random)
                {
                    videoFrame.timecode = random.Next();
                }

                NDIlib.send_send_video_v2(senderPtr, ref videoFrame);
                frame++;
                iterations++;

                frame = frame % frameRate;

                lastLoopRunTime = DateTime.Now;
            }

            NDIlib.send_destroy(senderPtr);
        });


        Console.WriteLine("Chaos Generator for NDI v2025.1.31.1 Started - q to exit, ? for command list.");
        while (true)
        {
            Console.Write("Command > ");
            var input = Console.ReadLine();

            try
            {
                if (input == "q")
                {
                    cts.Cancel();
                    break;
                }
                else if (input == "s")
                {
                    stallRequested = true;
                }
                else if (input.StartsWith("s "))
                {
                    var stallTimeRequested = int.Parse(input.Split(" ").LastOrDefault());
                    stallRequestTime = stallTimeRequested;
                    stallRequested = true;
                }
                else if (input.StartsWith("j"))
                {
                    if (allowNdiClockVideo)
                    {
                        Console.WriteLine("WARNING: This has no effect if the launch clock mode is not clock=override");
                    }
                    else
                    {
                        clockJitterLowerBound = int.Parse(input.Split(" ")[1]);
                        clockJitterUpperBound = int.Parse(input.Split(" ")[2]);
                        Console.WriteLine($"\tNew clock jitter is between {clockJitterLowerBound} to {clockJitterUpperBound} msec.");
                    }
                }
                else if (input.StartsWith("t "))
                {
                    switch (input.Split(" ")[1])
                    {
                        case "c":
                            timecodeMode = TimeCodeMode.SystemClock;
                            break;
                        case "s":
                            timecodeMode = TimeCodeMode.Synthesize;
                            break;
                        case "i":
                            timecodeMode = TimeCodeMode.Invalid;
                            break;
                        case "o":
                            timecodeMode = TimeCodeMode.FrameCounter;
                            break;
                        case "r":
                            timecodeMode = TimeCodeMode.Random;
                            break;
                        default:
                            timecodeMode = TimeCodeMode.Synthesize;
                            break;
                    }

                    Console.WriteLine($"Timecode mode: {timecodeMode}");
                }
                else if (input == "?")
                {
                    Console.WriteLine(@"Command List:

q: Quit the application.

s [time]: Request a stall, where [time] is the number of milliseconds the sender should wait.

j [low] [high]: Set the jitter for the source, if the clock type is not override (at launch).

t [type]: Set the timecode source. [type] can be:
  c: System clock (ticks) - uses DateTime.Now.Ticks.
  s: Synthesize (default) - lets NDI create a timecode.
  i: Invalid - uses the integer 1 as the timecode for every frame.
  o: Frame counter - uses the # of frames output since launch as the timecode.
  r: Random - generates a pseudorandom number as the timecode.

?: List out commands (this screen).");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error when executing command: " + ex.ToString());
            }
        }

        await ndiTask;

        for(var i = 0; i < frameRate; i++)
        {
            Marshal.FreeHGlobal(frameList[i]);
        }


    }
}
