using System.Text.Json;
using FileAssistant.Core.Engines;

namespace FileAssistant.Infrastructure.Engines.Media;

internal sealed class FfmpegConverter(string executable)
{
    internal async Task<IReadOnlyList<string>> CheckAsync(string path, ConversionRequest request, CancellationToken token)
    {
        var probe = Path.Combine(Path.GetDirectoryName(executable)!, "ffprobe.exe");
        var json = await LocalEngineProcess.RunAsync(probe,
            ["-v", "error", "-protocol_whitelist", "file,pipe", "-show_entries", "stream=codec_type,codec_name,channels:stream_tags=language,title", "-of", "json", path],
            TimeSpan.FromSeconds(30), token, "无法读取音视频文件，文件可能损坏或格式不支持。");
        using var document = JsonDocument.Parse(json);
        var streams = document.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        var required = ConversionCapabilities.IsVideoTarget(request.Target) ? "video" : "audio";
        if (!streams.Any(stream =>
            stream.TryGetProperty("codec_type", out var type) && type.GetString() == required))
            throw new FileProcessingException(required == "video" ? "文件没有可转换的视频流。" : "文件没有音轨，无法提取或转换音频。");
        var audio = streams.Where(stream => stream.GetProperty("codec_type").GetString() == "audio").ToArray();
        if (request.Options.AudioTrack > audio.Length && (audio.Length > 0 || required == "audio" || request.Options.AudioTrack != 1))
            throw new FileProcessingException($"所选第 {request.Options.AudioTrack} 音轨不存在；此文件共 {audio.Length} 条音轨。");
        return audio.Select((stream, index) => $"音轨 {index + 1} / {audio.Length} · {stream.GetProperty("codec_name").GetString()}" +
            (stream.TryGetProperty("tags", out var tags) ? " · " + string.Join(" / ", tags.EnumerateObject().Select(tag => tag.Value.GetString())) : "")).ToArray();
    }

    internal async Task ConvertAsync(string path, string output, ConversionRequest request, CancellationToken token)
    {
        var target = request.Target; var options = request.Options;
        await CheckAsync(path, request, token);
        var bitrate = options.AudioBitrateKbps + "k";
        var arguments = new List<string> { "-nostdin", "-hide_banner", "-loglevel", "error", "-xerror", "-n", "-protocol_whitelist", "file,pipe", "-i", path };
        if (ConversionCapabilities.IsAudioTarget(target))
        {
            arguments.AddRange(["-map", $"0:a:{options.AudioTrack - 1}", "-vn", "-sn", "-dn", "-map_metadata", "-1"]);
            arguments.AddRange(target switch
            {
                ConversionTarget.Mp3 => ["-c:a", "libmp3lame", "-b:a", bitrate, "-f", "mp3"],
                ConversionTarget.Wav => ["-c:a", "pcm_s16le", "-f", "wav"],
                ConversionTarget.Flac => ["-c:a", "flac", "-f", "flac"],
                ConversionTarget.M4a => ["-c:a", "aac", "-b:a", bitrate, "-movflags", "+faststart", "-f", "ipod"],
                ConversionTarget.Aac => ["-c:a", "aac", "-b:a", bitrate, "-f", "adts"],
                ConversionTarget.Ogg => ["-c:a", "libvorbis", "-q:a", "5", "-f", "ogg"],
                _ => throw new FileProcessingException("不支持此音频输出格式。")
            });
        }
        else
        {
            var resize = options.MaximumEdge > 0 ? $"scale=w='min(iw,{options.MaximumEdge})':h='min(ih,{options.MaximumEdge})':force_original_aspect_ratio=decrease:force_divisible_by=2:reset_sar=1," : "";
            arguments.AddRange(["-map", "0:V:0", "-map", $"0:a:{options.AudioTrack - 1}?", "-sn", "-dn", "-map_metadata", "-1",
                "-vf", resize + "pad=ceil(iw/2)*2:ceil(ih/2)*2", "-pix_fmt", "yuv420p"]);
            arguments.AddRange(target == ConversionTarget.WebM
                ? ["-c:v", "libvpx-vp9", "-b:v", options.VideoBitrateKbps + "k", "-deadline", "good", "-cpu-used", "4", "-c:a", "libopus", "-b:a", bitrate, "-f", "webm"]
                : ["-c:v", "libopenh264", "-b:v", options.VideoBitrateKbps + "k", "-c:a", "aac", "-b:a", bitrate, "-f", target switch
                    { ConversionTarget.Mp4 => "mp4", ConversionTarget.Mov => "mov", ConversionTarget.Mkv => "matroska", _ => throw new FileProcessingException("不支持此视频输出格式。") }]);
        }
        if (options.SampleRate > 0 && target != ConversionTarget.WebM) arguments.AddRange(["-ar", options.SampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        arguments.Add(output);
        await LocalEngineProcess.RunAsync(executable, arguments, TimeSpan.FromHours(24), token, "音视频转换失败，文件可能损坏或所选编码不支持此输入。");
        if (!File.Exists(output) || new FileInfo(output).Length == 0) throw new FileProcessingException("音视频引擎没有生成有效输出。");
    }
}
