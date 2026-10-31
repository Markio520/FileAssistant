using System.Text.Json;
using FileAssistant.Core.Engines;

namespace FileAssistant.Infrastructure.Engines.Media;

internal sealed class FfmpegConverter(string executable)
{
    internal async Task CheckAsync(string path, CancellationToken token)
    {
        var probe = Path.Combine(Path.GetDirectoryName(executable)!, "ffprobe.exe");
        var json = await LocalEngineProcess.RunAsync(probe,
            ["-v", "error", "-protocol_whitelist", "file,pipe", "-show_entries", "stream=codec_type", "-of", "json", path],
            TimeSpan.FromSeconds(30), token, "无法读取音视频文件，文件可能损坏或格式不支持。");
        using var document = JsonDocument.Parse(json);
        var required = ConversionCapabilities.IsVideo(path) ? "video" : "audio";
        if (!document.RootElement.GetProperty("streams").EnumerateArray().Any(stream =>
            stream.TryGetProperty("codec_type", out var type) && type.GetString() == required))
            throw new FileProcessingException(required == "video" ? "文件没有可转换的视频流。" : "文件没有可转换的音频流。");
    }

    internal async Task ConvertAsync(string path, string output, ConversionTarget target, CancellationToken token)
    {
        var arguments = new List<string> { "-nostdin", "-hide_banner", "-loglevel", "error", "-xerror", "-n", "-protocol_whitelist", "file,pipe", "-i", path };
        if (ConversionCapabilities.IsAudioTarget(target))
        {
            arguments.AddRange(["-map", "0:a:0", "-vn", "-sn", "-dn", "-map_metadata", "-1"]);
            arguments.AddRange(target switch
            {
                ConversionTarget.Mp3 => ["-c:a", "libmp3lame", "-q:a", "2", "-f", "mp3"],
                ConversionTarget.Wav => ["-c:a", "pcm_s16le", "-f", "wav"],
                ConversionTarget.Flac => ["-c:a", "flac", "-f", "flac"],
                ConversionTarget.M4a => ["-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", "-f", "ipod"],
                ConversionTarget.Aac => ["-c:a", "aac", "-b:a", "192k", "-f", "adts"],
                ConversionTarget.Ogg => ["-c:a", "libvorbis", "-q:a", "5", "-f", "ogg"],
                _ => throw new FileProcessingException("不支持此音频输出格式。")
            });
        }
        else
        {
            arguments.AddRange(["-map", "0:V:0", "-map", "0:a:0?", "-sn", "-dn", "-map_metadata", "-1",
                "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2", "-pix_fmt", "yuv420p"]);
            arguments.AddRange(target == ConversionTarget.WebM
                ? ["-c:v", "libvpx-vp9", "-crf", "32", "-b:v", "0", "-deadline", "good", "-cpu-used", "4", "-c:a", "libopus", "-b:a", "128k", "-f", "webm"]
                : ["-c:v", "libopenh264", "-b:v", "4M", "-c:a", "aac", "-b:a", "192k", "-f", target switch
                    { ConversionTarget.Mp4 => "mp4", ConversionTarget.Mov => "mov", ConversionTarget.Mkv => "matroska", _ => throw new FileProcessingException("不支持此视频输出格式。") }]);
        }
        arguments.Add(output);
        await LocalEngineProcess.RunAsync(executable, arguments, TimeSpan.FromHours(24), token, "音视频转换失败，文件可能损坏或所选编码不支持此输入。");
        if (!File.Exists(output) || new FileInfo(output).Length == 0) throw new FileProcessingException("音视频引擎没有生成有效输出。");
    }
}
