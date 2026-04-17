using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageViewerUno.Services;

public sealed record AnimatedImageFrame(byte[] Bgra, int Width, int Height, int DurationMs);

/// <summary>
/// Decodes GIF + WebP (static or animated) into BGRA32 frame sequences using SixLabors.ImageSharp.
/// Replaces the source WPF app's native libwebp.dll P/Invoke + System.Drawing-based AnimatedWebpElement.
/// Runs on Windows, Skia Desktop (macOS/Linux/Windows), and WebAssembly — no native dependencies.
/// </summary>
public static class AnimatedImageDecoder
{
    public static IReadOnlyList<AnimatedImageFrame> Decode(Stream stream)
    {
        using var image = SixLabors.ImageSharp.Image.Load<Bgra32>(stream);
        int w = image.Width;
        int h = image.Height;
        var frames = new List<AnimatedImageFrame>(image.Frames.Count);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            var frame = image.Frames[i];
            var bytes = new byte[w * h * 4];
            frame.CopyPixelDataTo(bytes);
            frames.Add(new AnimatedImageFrame(bytes, w, h, GetFrameDurationMs(frame.Metadata)));
        }

        return frames;
    }

    private static int GetFrameDurationMs(ImageFrameMetadata metadata)
    {
        var gif = metadata.GetGifMetadata();
        if (gif is not null && gif.FrameDelay > 0)
        {
            return gif.FrameDelay * 10; // GIF stores 1/100ths of a second.
        }

        var webp = metadata.GetWebpMetadata();
        if (webp is not null && webp.FrameDelay > 0)
        {
            return (int)webp.FrameDelay; // WebP stores ms directly (uint).
        }

        return 100; // Fallback: 100 ms per frame for static images with no duration.
    }
}
