namespace FileAssistant.Core.Engines;

/// <summary>Plans actual outputs, including many-to-one PDF creation and one-to-many page export.</summary>
public static class ConversionPlanner
{
    public static ConversionPlan Create(ConversionRequest request, IReadOnlyList<ProcessingSource> sources, Func<string, bool> pathExists)
    {
        if (!Path.IsPathFullyQualified(request.OutputDirectory) || request.OutputDirectory.Contains("://", StringComparison.Ordinal))
            throw new FileProcessingException("请选择完整的本地输出文件夹路径。");
        if (!Enum.IsDefined(request.Target)) throw new FileProcessingException("不支持此输出格式。");
        if (request.Quality is < 1 or > 100) throw new FileProcessingException("图片质量应在 1–100 之间。");
        if (request.PdfDpi is < 72 or > 600) throw new FileProcessingException("PDF 导出分辨率应在 72–600 DPI 之间。");
        if (sources.Count == 0) throw new FileProcessingException("请先添加要转换的文件。");
        request.Options.Validate();
        var items = new List<ConversionPlanItem>();
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Even an input that does not exist at execution time must never become an output target.
        foreach (var source in sources.Where(source => source.Error is null)) reserved.Add(Path.GetFullPath(source.Path));
        string Reserve(string name)
        {
            var stem = Path.GetFileNameWithoutExtension(name);
            var extension = Path.GetExtension(name);
            var candidate = Path.Combine(request.OutputDirectory, name);
            for (var suffix = 1; reserved.Contains(candidate) || pathExists(candidate); suffix++)
                candidate = Path.Combine(request.OutputDirectory, $"{stem} ({suffix}){extension}");
            reserved.Add(candidate);
            return candidate;
        }
        var eligible = sources.Where(source => source.Error is null && (request.Options.Mode == ConversionMode.Process
            ? ConversionCapabilities.GetProcessingTargets(source.Path) : ConversionCapabilities.GetTargets(source.Path, includeSameFormat: true)).Contains(request.Target)).ToArray();
        foreach (var source in sources.Where(source => source.Error is not null))
            items.Add(new([source], null, Error: source.Error));
        foreach (var source in sources.Where(source => source.Error is null && !eligible.Contains(source)))
            items.Add(new([source], null, Error: "此文件不能转换为所选格式，请按类型分批转换。"));
        if (request.Target == ConversionTarget.Pdf)
        {
            foreach (var document in eligible.Where(source => ConversionCapabilities.IsOffice(source.Path)))
                items.Add(new([document], Reserve(Path.GetFileNameWithoutExtension(document.Path) + ".pdf")));
            var images = eligible.Where(source => ConversionCapabilities.IsImage(source.Path)).ToArray();
            if (images.Length > 0) items.Add(new(images, Reserve("合并图片.pdf"), Detail: string.Join("；", images.Select(source =>
                Path.GetFileName(source.Path) + (ConversionCapabilities.IsTiff(source.Path) && request.Options.AllTiffPages ? $" 全部 {source.Frames} 页" : $" 第 {request.Options.Frame} 帧/页")))));
        }
        else
        {
            var extension = ConversionCapabilities.Extension(request.Target);
            foreach (var source in eligible)
            {
                var stem = Path.GetFileNameWithoutExtension(source.Path);
                if (request.Target is ConversionTarget.Csv or ConversionTarget.Tsv)
                {
                    var sheets = source.Sheets ?? [];
                    if (sheets.Count == 0 || request.Options.Sheet > sheets.Count)
                    { items.Add(new([source], null, Error: $"工作表不存在（共 {sheets.Count} 表），请重新选择。")); continue; }
                    for (var sheet = 0; sheet < sheets.Count; sheet++)
                    {
                        if (request.Options.Sheet != 0 && request.Options.Sheet != sheet + 1) continue;
                        var safeName = string.Concat(sheets[sheet].Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
                        if (safeName.Length > 60) safeName = safeName[..60];
                        items.Add(new([source], Reserve($"{stem}_表{sheet + 1:D3}_{safeName}{extension}"), Sheet: sheet + 1,
                            Detail: $"工作表 {sheet + 1}：{sheets[sheet]} · {request.Options.Encoding} · {(request.Target == ConversionTarget.Tsv ? "Tab" : request.Options.Separator)} · {(request.Options.ExportFormulas ? "公式" : "计算结果")}"));
                    }
                }
                else if (source.PdfPages > 0 && request.Target != ConversionTarget.Txt)
                    for (var page = 0; page < source.PdfPages; page++)
                        items.Add(new([source], Reserve($"{stem}_第{page + 1:D3}页{extension}"), page));
                else if (ConversionCapabilities.IsTiff(source.Path) && request.Options.AllTiffPages)
                    for (var page = 0; page < source.Frames; page++)
                        items.Add(new([source], Reserve($"{stem}_第{page + 1:D3}页{extension}"), ImageFrame: page, Detail: $"第 {page + 1}/{source.Frames} 页"));
                else if (ConversionCapabilities.IsImage(source.Path) && !ConversionCapabilities.IsAnimation(source.Path, request.Target))
                {
                    if (request.Options.Frame > source.Frames) items.Add(new([source], null, Error: $"帧/页不存在（共 {source.Frames} 帧/页）。"));
                    else items.Add(new([source], Reserve(stem + extension), ImageFrame: request.Options.Frame - 1,
                        Detail: request.Target == ConversionTarget.Ico ? "16/24/32/48/64/128/256 像素" : $"第 {request.Options.Frame}/{source.Frames} 帧/页"));
                }
                else items.Add(new([source], Reserve(stem + extension), Detail: ConversionCapabilities.IsAnimation(source.Path, request.Target) ? $"全部 {source.Frames} 帧（保留时序、循环）"
                    : source.AudioTracks is { Count: > 0 } tracks && request.Options.AudioTrack <= tracks.Count ? tracks[request.Options.AudioTrack - 1] : null));
            }
        }
        return new(request, items);
    }
}
