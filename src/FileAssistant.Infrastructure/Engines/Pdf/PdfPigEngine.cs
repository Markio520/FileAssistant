using FileAssistant.Core.Engines;
using FileAssistant.Infrastructure.Engines.Images;
using ImageMagick;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using System.Text;

namespace FileAssistant.Infrastructure.Engines.Pdf;

internal static class PdfPigEngine
{
    internal static void Create(IReadOnlyList<ProcessingSource> images, Stream output, CancellationToken token, ConversionSettings? options = null)
    {
        using var builder = new PdfDocumentBuilder();
        foreach (var source in images)
        {
            var settings = options ?? new();
            var frames = ConversionCapabilities.IsTiff(source.Path) && settings.AllTiffPages
                ? Enumerable.Range(0, source.Frames) : [settings.Frame - 1];
            foreach (var frame in frames)
            {
            token.ThrowIfCancellationRequested();
            using var image = MagickImageEngine.Read(source.Path, frame);
            var landscape = image.Width > image.Height;
            var pageWidth = landscape ? 841.89 : 595.28;
            var pageHeight = landscape ? 595.28 : 841.89;
            var page = builder.AddPage(pageWidth, pageHeight);
            var scale = Math.Min((pageWidth - 48) / image.Width, (pageHeight - 48) / image.Height);
            var width = image.Width * scale; var height = image.Height * scale;
            var left = (pageWidth - width) / 2; var bottom = (pageHeight - height) / 2;
            page.AddPng(image.ToByteArray(MagickFormat.Png), new PdfRectangle(left, bottom, left + width, bottom + height));
            }
        }
        token.ThrowIfCancellationRequested();
        output.Write(builder.Build());
    }

    internal static void ExtractText(string path, Stream output, CancellationToken token)
    {
        using var pdf = PdfDocument.Open(path);
        var pages = new List<string>();
        var hasText = false;
        foreach (var page in pdf.GetPages())
        {
            token.ThrowIfCancellationRequested();
            var words = page.GetWords(NearestNeighbourWordExtractor.Instance);
            var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
            var ordered = UnsupervisedReadingOrderDetector.Instance.Get(blocks);
            var text = string.Join(Environment.NewLine + Environment.NewLine, ordered.Select(block => block.Text));
            text = new string(text.Where(character => !char.IsControl(character) || character is '\r' or '\n' or '\t').ToArray());
            var meaningful = text.Any(char.IsLetterOrDigit);
            hasText |= meaningful;
            pages.Add(meaningful ? text.Trim() : $"[第 {page.Number} 页未提取到有效文字，可能为空白或扫描页]");
        }
        if (!hasText) throw new FileProcessingException("PDF 未提取到有效文字，可能是扫描件或文字编码不受支持；本阶段不包含 OCR。未生成空 TXT。");
        using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        writer.Write(string.Join(Environment.NewLine + "\f" + Environment.NewLine, pages));
        writer.Flush();
    }
}
