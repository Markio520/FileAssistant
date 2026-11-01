using CommunityToolkit.Mvvm.ComponentModel;
using FileAssistant.App.Controls;
using FileAssistant.Core.Engines;

namespace FileAssistant.App.Features.Convert;

public partial class ConversionViewModel
{
    public IReadOnlyList<SelectionOption> Modes { get; } = [new(0, "格式转换"), new(1, "压缩 / 兼容转换")];
    public IReadOnlyList<SelectionOption> Presets { get; } = [new(0, "均衡 · 保留尺寸"), new(1, "较小文件 · 最长边 1280"), new(2, "兼容优先 · 最长边 1920")];
    public IReadOnlyList<SelectionOption> Encodings { get; } = [new(0, "UTF-8"), new(1, "GB18030 · 简体中文")];
    public IReadOnlyList<SelectionOption> Separators { get; } = [new(0, "逗号 ,"), new(1, "分号 ;"), new(2, "Tab 制表符")];
    public IReadOnlyList<SelectionOption> SampleRates { get; } = [new(0, "保留采样率"), new(22050, "22050 Hz"), new(44100, "44100 Hz"), new(48000, "48000 Hz")];
    [ObservableProperty] public partial int ModeValue { get; set; }
    [ObservableProperty] public partial int PresetValue { get; set; }
    [ObservableProperty] public partial double MaximumEdge { get; set; }
    [ObservableProperty] public partial double AudioTrack { get; set; } = 1;
    [ObservableProperty] public partial double AudioBitrate { get; set; } = 192;
    [ObservableProperty] public partial double VideoBitrate { get; set; } = 4000;
    [ObservableProperty] public partial int SampleRate { get; set; }
    [ObservableProperty] public partial double Frame { get; set; } = 1;
    [ObservableProperty] public partial bool AllTiffPages { get; set; } = true;
    [ObservableProperty] public partial double Sheet { get; set; }
    [ObservableProperty] public partial int EncodingValue { get; set; }
    [ObservableProperty] public partial int SeparatorValue { get; set; }
    [ObservableProperty] public partial bool ExportFormulas { get; set; }
    [ObservableProperty] public partial bool ImportFormulas { get; set; }

    private ConversionTarget Target => (ConversionTarget)TargetFormatValue;
    public bool ShowProcessing => ModeValue == (int)ConversionMode.Process;
    public bool ShowSize => HasFormats && (ConversionCapabilities.IsImageTarget(Target) || ConversionCapabilities.IsVideoTarget(Target));
    public bool ShowMedia => HasFormats && (ConversionCapabilities.IsAudioTarget(Target) || ConversionCapabilities.IsVideoTarget(Target));
    public bool ShowVideo => HasFormats && ConversionCapabilities.IsVideoTarget(Target);
    public bool ShowAudioBitrate => ShowMedia && Target is not (ConversionTarget.Wav or ConversionTarget.Flac or ConversionTarget.Ogg);
    public bool ShowSampleRate => ShowMedia && Target != ConversionTarget.WebM;
    public bool ShowTiffPages => HasFormats && ConversionEntries.Any(file => ConversionCapabilities.IsTiff(file.FullPath));
    public bool ShowFrame => HasFormats && ConversionEntries.Any(file => ConversionCapabilities.IsImage(file.FullPath) &&
        !ConversionCapabilities.IsAnimation(file.FullPath, Target) && !(ConversionCapabilities.IsTiff(file.FullPath) && AllTiffPages));
    public bool ShowSheet => Target is ConversionTarget.Csv or ConversionTarget.Tsv;
    public bool ShowDelimited => ShowSheet || ShowImportFormulas;
    public bool ShowImportFormulas => ConversionEntries.Any(file => file.Extension.ToLowerInvariant() is "csv" or "tsv");
    public bool ShowSeparator => Target == ConversionTarget.Csv || ConversionEntries.Any(file => file.Extension.Equals("csv", StringComparison.OrdinalIgnoreCase));

    private void NotifyOptions()
    {
        foreach (var name in new[] { nameof(ShowProcessing), nameof(ShowSize), nameof(ShowMedia), nameof(ShowVideo), nameof(ShowAudioBitrate), nameof(ShowSampleRate),
            nameof(ShowTiffPages), nameof(ShowFrame), nameof(ShowSheet), nameof(ShowDelimited), nameof(ShowImportFormulas), nameof(ShowSeparator), nameof(ConversionNote), nameof(ShowPdfDpi) }) OnPropertyChanged(name);
    }
    partial void OnModeValueChanged(int value) { RefreshInput(force: true); NotifyOptions(); }
    partial void OnPresetValueChanged(int value)
    {
        Quality = value == 1 ? 75 : 90;
        MaximumEdge = value == 1 ? 1280 : value == 2 ? 1920 : 0;
        AudioBitrate = value == 1 ? 128 : 192; VideoBitrate = value == 1 ? 1500 : 4000;
        SampleRate = value == 2 ? 48000 : 0; Invalidate();
    }
    partial void OnMaximumEdgeChanged(double value) => Invalidate();
    partial void OnAudioTrackChanged(double value) => Invalidate();
    partial void OnAudioBitrateChanged(double value) => Invalidate();
    partial void OnVideoBitrateChanged(double value) => Invalidate();
    partial void OnSampleRateChanged(int value) => Invalidate();
    partial void OnFrameChanged(double value) => Invalidate();
    partial void OnAllTiffPagesChanged(bool value) { Invalidate(); NotifyOptions(); }
    partial void OnSheetChanged(double value) => Invalidate();
    partial void OnEncodingValueChanged(int value) => Invalidate();
    partial void OnSeparatorValueChanged(int value) => Invalidate();
    partial void OnExportFormulasChanged(bool value) => Invalidate();
    partial void OnImportFormulasChanged(bool value) => Invalidate();

    private ConversionSettings ReadSettings()
    {
        static int Number(double value)
        {
            if (!double.IsFinite(value) || value != Math.Truncate(value) || value < 0 || value > int.MaxValue) throw new FileProcessingException("请填写有效的整数参数。");
            return (int)value;
        }
        var settings = new ConversionSettings((ConversionMode)ModeValue, ShowSize ? Number(MaximumEdge) : 0,
            ShowMedia ? Number(AudioTrack) : 1, ShowAudioBitrate ? Number(AudioBitrate) : 192, ShowVideo ? Number(VideoBitrate) : 4000,
            ShowSampleRate ? SampleRate : 0, ShowFrame ? Number(Frame) : 1, AllTiffPages, ShowSheet ? Number(Sheet) : 0,
            (DelimitedEncoding)EncodingValue, (CsvSeparator)SeparatorValue, ExportFormulas, ImportFormulas);
        settings.Validate(); return settings;
    }
}
