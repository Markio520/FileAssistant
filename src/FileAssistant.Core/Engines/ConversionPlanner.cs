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
        var eligible = sources.Where(source => source.Error is null).ToArray();
        foreach (var source in sources.Where(source => source.Error is not null))
            items.Add(new([source], null, Error: source.Error));
        if (request.Target == ConversionTarget.Pdf)
        {
            foreach (var pdf in eligible.Where(source => source.PdfPages > 0))
                items.Add(new([pdf], null, Error: "生成 PDF 当前只接受图片；PDF 可导出为图片。"));
            var images = eligible.Where(source => source.PdfPages == 0).ToArray();
            if (images.Length > 0) items.Add(new(images, Reserve("合并图片.pdf")));
        }
        else
        {
            var extension = request.Target switch { ConversionTarget.Jpeg => ".jpg", ConversionTarget.WebP => ".webp", ConversionTarget.Bmp => ".bmp", _ => ".png" };
            foreach (var source in eligible)
            {
                var stem = Path.GetFileNameWithoutExtension(source.Path);
                if (source.PdfPages > 0)
                    for (var page = 0; page < source.PdfPages; page++)
                        items.Add(new([source], Reserve($"{stem}_第{page + 1:D3}页{extension}"), page));
                else items.Add(new([source], Reserve(stem + extension)));
            }
        }
        return new(request, items);
    }
}
