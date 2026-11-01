using System.Text;
using FileAssistant.Core.Engines;
using ImageMagick;

namespace FileAssistant.Infrastructure.Engines.Images;

/// <summary>Full-canvas WebP muxing; Magick still encodes every frame. Avoids native animation
/// optimization merging duplicate frames or cropping away the transparent canvas.</summary>
internal static class WebpAnimationWriter
{
    internal static void Write(MagickImageCollection frames, Stream output, CancellationToken token)
    {
        using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        void FourCc(string value) => writer.Write(Encoding.ASCII.GetBytes(value));
        void UInt24(uint value) { writer.Write((byte)value); writer.Write((byte)(value >> 8)); writer.Write((byte)(value >> 16)); }
        var width = frames[0].Width; var height = frames[0].Height;
        if (width > 16383 || height > 16383 || frames[0].AnimationIterations > ushort.MaxValue)
            throw new FileProcessingException("WebP 尺寸或循环次数超出格式限制。");
        FourCc("RIFF"); writer.Write(0u); FourCc("WEBP");
        FourCc("VP8X"); writer.Write(10u); writer.Write(0x12u); UInt24(width - 1); UInt24(height - 1);
        FourCc("ANIM"); writer.Write(6u); writer.Write(0u); writer.Write((ushort)frames[0].AnimationIterations);
        foreach (var frame in frames)
        {
            token.ThrowIfCancellationRequested();
            if (frame.Width != width || frame.Height != height) throw new FileProcessingException("动画帧画布尺寸不一致。");
            var duration = (uint)Math.Round(frame.AnimationDelay * 1000.0 / Math.Max(1, frame.AnimationTicksPerSecond));
            if (duration > 0xffffff) throw new FileProcessingException("动画单帧时长超出 WebP 限制。");
            var encoded = frame.ToByteArray(MagickFormat.WebP);
            using var payload = new MemoryStream();
            for (var offset = 12; offset + 8 <= encoded.Length;)
            {
                var size = BitConverter.ToUInt32(encoded, offset + 4);
                var length = checked(8 + (int)size + (int)(size & 1));
                if (offset + length > encoded.Length) throw new FileProcessingException("WebP 编码数据不完整。");
                var id = Encoding.ASCII.GetString(encoded, offset, 4);
                if (id is "ALPH" or "VP8 " or "VP8L") payload.Write(encoded, offset, length);
                offset += length;
            }
            if (payload.Length == 0) throw new FileProcessingException("WebP 引擎未生成帧数据。");
            FourCc("ANMF"); writer.Write(checked((uint)payload.Length + 16));
            UInt24(0); UInt24(0); UInt24(width - 1); UInt24(height - 1); UInt24(duration);
            writer.Write((byte)3); // Replace, then dispose to transparent before the next full canvas.
            payload.Position = 0; payload.CopyTo(output);
        }
        var end = output.Position;
        output.Position = 4; writer.Write(checked((uint)(end - 8))); output.Position = end;
    }
}
