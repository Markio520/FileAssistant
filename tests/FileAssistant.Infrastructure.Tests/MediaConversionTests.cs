using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FileAssistant.Core.Engines;
using FileAssistant.Infrastructure.Engines;

namespace FileAssistant.Infrastructure.Tests;

public sealed class MediaConversionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "FileAssistant-media-test-" + Guid.NewGuid().ToString("N"));
    private readonly string ffmpeg;
    private readonly LocalFileProcessingService service;
    public MediaConversionTests()
    {
        Directory.CreateDirectory(root);
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "FileAssistant.slnx"))) repository = repository.Parent;
        ffmpeg = Path.Combine(repository!.FullName, "artifacts", "dependencies", "ffmpeg", EnginePackageCatalog.Ffmpeg.Version, "x64", "bundle", "bin", "ffmpeg.exe");
        service = new(new(ffmpeg, Path.Combine(root, "cache")));
    }

    [Theory]
    [InlineData(ConversionTarget.Mp3, "mp3")]
    [InlineData(ConversionTarget.Wav, "pcm_s16le")]
    [InlineData(ConversionTarget.Flac, "flac")]
    [InlineData(ConversionTarget.M4a, "aac")]
    [InlineData(ConversionTarget.Aac, "aac")]
    [InlineData(ConversionTarget.Ogg, "vorbis")]
    public async Task Audio_outputs_contain_real_decodable_audio_and_keep_sources(ConversionTarget target, string codec)
    {
        var source = Path.Combine(root, "声音 中文 & 空格.wav");
        await Run(ffmpeg, ["-v", "error", "-f", "lavfi", "-i", "sine=frequency=440:duration=0.4", source]);
        var hash = SHA256.HashData(await File.ReadAllBytesAsync(source));
        var plan = await service.CreatePlanAsync(new([source], Path.Combine(root, "out"), target), default);
        Assert.Equal(1, plan.ReadyCount);
        var report = await service.ConvertAsync(plan, null, default);
        var output = Assert.Single(report.Outputs);
        await AssertStreams(output.FullPath, codec);
        var roundtrip = await service.CreatePlanAsync(new([output.FullPath], Path.Combine(root, "roundtrip"), ConversionTarget.Wav), default);
        var wave = Assert.Single((await service.ConvertAsync(roundtrip, null, default)).Outputs);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString((await File.ReadAllBytesAsync(wave.FullPath))[..4]));
        Assert.Equal(hash, SHA256.HashData(await File.ReadAllBytesAsync(source)));
    }

    [Theory]
    [InlineData(ConversionTarget.Mp4, "h264")]
    [InlineData(ConversionTarget.Mkv, "h264")]
    [InlineData(ConversionTarget.Mov, "h264")]
    [InlineData(ConversionTarget.WebM, "vp9")]
    public async Task Video_outputs_keep_video_and_audio_in_compatible_containers(ConversionTarget target, string codec)
    {
        var source = Path.Combine(root, "视频 中文.mkv");
        await Run(ffmpeg, ["-v", "error", "-f", "lavfi", "-i", "color=red:size=128x72:rate=10:duration=0.4", "-f", "lavfi", "-i", "sine=duration=0.4",
            "-c:v", "mpeg4", "-c:a", "flac", "-shortest", source]);
        var hash = SHA256.HashData(await File.ReadAllBytesAsync(source));
        var plan = await service.CreatePlanAsync(new([source], Path.Combine(root, "out"), target), default);
        Assert.Equal(1, plan.ReadyCount);
        var report = await service.ConvertAsync(plan, null, default);
        Assert.True(report.Outputs.Count == 1, string.Join(";", report.Items.Select(item => item.Message)));
        var output = Assert.Single(report.Outputs);
        await AssertStreams(output.FullPath, codec, video: true);
        var roundtrip = await service.CreatePlanAsync(new([output.FullPath], Path.Combine(root, "roundtrip"), ConversionTarget.Mp4), default);
        Assert.Single((await service.ConvertAsync(roundtrip, null, default)).Outputs);
        Assert.Equal(hash, SHA256.HashData(await File.ReadAllBytesAsync(source)));
    }

    [Fact]
    public async Task Malformed_media_is_rejected_and_cancellation_leaves_no_partial_outputs()
    {
        var bad = Path.Combine(root, "broken.mp3"); await File.WriteAllTextAsync(bad, "not audio");
        var invalid = await service.CreatePlanAsync(new([bad], root, ConversionTarget.Wav), default);
        Assert.Equal(1, invalid.RejectedCount); Assert.Equal(0, invalid.ReadyCount);
        var source = Path.Combine(root, "cancel.mkv");
        await Run(ffmpeg, ["-v", "error", "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=30:duration=4", "-c:v", "mpeg4", source]);
        var plan = await service.CreatePlanAsync(new([source], Path.Combine(root, "cancelled"), ConversionTarget.WebM), default);
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(100);
        var report = await service.ConvertAsync(plan, null, cancellation.Token);
        Assert.True(report.Cancelled); Assert.Empty(report.Outputs);
        Assert.False(Directory.Exists(plan.Request.OutputDirectory) && Directory.EnumerateFiles(plan.Request.OutputDirectory).Any());
    }

    private async Task AssertStreams(string path, string codec, bool video = false)
    {
        var json = await Run(Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe"), ["-v", "error", "-show_streams", "-of", "json", path]);
        using var document = JsonDocument.Parse(json);
        var streams = document.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        Assert.Contains(streams, stream => stream.GetProperty("codec_name").GetString() == codec);
        if (video)
        {
            Assert.Contains(streams, stream => stream.GetProperty("codec_type").GetString() == "audio");
            var image = Assert.Single(streams, stream => stream.GetProperty("codec_type").GetString() == "video");
            Assert.Equal(128, image.GetProperty("width").GetInt32()); Assert.Equal(72, image.GetProperty("height").GetInt32());
        }
        await Run(ffmpeg, ["-v", "error", "-xerror", "-i", path, "-f", "null", "-"]);
    }
    private static async Task<string> Run(string executable, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync(); var output = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await error);
        return await output;
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
