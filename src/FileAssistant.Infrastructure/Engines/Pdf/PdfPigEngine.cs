using FileAssistant.Core.Engines;
using FileAssistant.Infrastructure.Engines.Images;
using ImageMagick;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace FileAssistant.Infrastructure.Engines.Pdf;

internal static class PdfPigEngine
{
    internal static void Create(IReadOnlyList<ProcessingSource> images, Stream output, CancellationToken token)
    {
        using var builder = new PdfDocumentBuilder();
        foreach (var source in images)
        {
            token.ThrowIfCancellationRequested();
            using var image = MagickImageEngine.Read(source.Path);
            var landscape = image.Width > image.Height;
            var pageWidth = landscape ? 841.89 : 595.28;
            var pageHeight = landscape ? 595.28 : 841.89;
            var page = builder.AddPage(pageWidth, pageHeight);
            var scale = Math.Min((pageWidth - 48) / image.Width, (pageHeight - 48) / image.Height);
            var width = image.Width * scale; var height = image.Height * scale;
            var left = (pageWidth - width) / 2; var bottom = (pageHeight - height) / 2;
            page.AddPng(image.ToByteArray(MagickFormat.Png), new PdfRectangle(left, bottom, left + width, bottom + height));
        }
        token.ThrowIfCancellationRequested();
        output.Write(builder.Build());
    }
}
