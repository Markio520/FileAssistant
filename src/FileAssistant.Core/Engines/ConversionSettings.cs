namespace FileAssistant.Core.Engines;

public enum ConversionMode { Convert, Process }
public enum DelimitedEncoding { Utf8, Gb18030 }
public enum CsvSeparator { Comma, Semicolon, Tab }

/// <summary>One-based sheet/frame/track numbers; zero sheet means all sheets. Zero edge/sample rate preserves the source.</summary>
public sealed record ConversionSettings(
    ConversionMode Mode = ConversionMode.Convert,
    int MaximumEdge = 0,
    int AudioTrack = 1,
    int AudioBitrateKbps = 192,
    int VideoBitrateKbps = 4000,
    int SampleRate = 0,
    int Frame = 1,
    bool AllTiffPages = true,
    int Sheet = 0,
    DelimitedEncoding Encoding = DelimitedEncoding.Utf8,
    CsvSeparator Separator = CsvSeparator.Comma,
    bool ExportFormulas = false,
    bool ImportFormulas = false)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Mode) || !Enum.IsDefined(Encoding) || !Enum.IsDefined(Separator)) throw new FileProcessingException("不支持此转换参数。");
        if (MaximumEdge is < 0 or > 16384 || MaximumEdge == 1) throw new FileProcessingException("最长边应为 0（原尺寸）或 2–16384 像素。");
        if (AudioTrack < 1 || Frame < 1 || Sheet < 0) throw new FileProcessingException("音轨和帧/页从 1 开始；工作表为 0 时导出全部。");
        if (AudioBitrateKbps is < 32 or > 320 || VideoBitrateKbps is < 100 or > 100000) throw new FileProcessingException("音频码率应为 32–320，视频码率应为 100–100000 kbps。");
        if (SampleRate is not (0 or 22050 or 44100 or 48000)) throw new FileProcessingException("采样率应为原始值、22050、44100 或 48000 Hz。");
    }
}
