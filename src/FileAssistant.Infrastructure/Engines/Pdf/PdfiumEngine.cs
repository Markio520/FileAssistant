using System.Runtime.InteropServices;
using FileAssistant.Core.Engines;
using ImageMagick;
using PDFiumCore;

namespace FileAssistant.Infrastructure.Engines.Pdf;

/// <summary>PDFium is serialized across all preview and export calls.</summary>
internal static class PdfiumEngine
{
    private static readonly object Gate = new();
    private static bool initialized;
    internal static void EnsureAvailable()
    { lock (Gate) { if (!initialized) { fpdfview.FPDF_InitLibrary(); initialized = true; } } }

    private static FpdfDocumentT Open(string path)
    {
        EnsureAvailable();
        var document = fpdfview.FPDF_LoadDocument(path, null);
        if (document is null || document.__Instance == IntPtr.Zero)
            throw new FileProcessingException("无法读取 PDF，文件可能损坏或需要密码。");
        return document;
    }

    internal static int PageCount(string path)
    {
        lock (Gate)
        {
            var document = Open(path);
            try
            {
                var pages = fpdfview.FPDF_GetPageCount(document);
                if (pages is < 1 or > 10_000) throw new FileProcessingException("PDF 页数无效或超过当前 10,000 页限制。");
                return pages;
            }
            finally { fpdfview.FPDF_CloseDocument(document); }
        }
    }

    internal static MagickImage Render(string path, int pageIndex, int dpi, int? maximumEdge, CancellationToken token)
    {
        lock (Gate)
        {
            token.ThrowIfCancellationRequested();
            var document = Open(path);
            try
            {
                var page = fpdfview.FPDF_LoadPage(document, pageIndex);
                if (page is null || page.__Instance == IntPtr.Zero) throw new FileProcessingException("无法读取此 PDF 页面。");
                try
                {
                    var width = fpdfview.FPDF_GetPageWidth(page) * dpi / 72d;
                    var height = fpdfview.FPDF_GetPageHeight(page) * dpi / 72d;
                    if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
                        throw new FileProcessingException("PDF 页面尺寸无效。");
                    if (maximumEdge is { } edge)
                    {
                        var scale = Math.Min(1, edge / Math.Max(width, height));
                        width *= scale; height *= scale;
                    }
                    if (width > int.MaxValue || height > int.MaxValue || width * height > 100_000_000)
                        throw new FileProcessingException("PDF 页面过大，请降低导出分辨率。");
                    var pixelWidth = Math.Max(1, (int)Math.Ceiling(width));
                    var pixelHeight = Math.Max(1, (int)Math.Ceiling(height));
                    var bitmap = fpdfview.FPDFBitmapCreateEx(pixelWidth, pixelHeight, 4, IntPtr.Zero, 0);
                    if (bitmap is null || bitmap.__Instance == IntPtr.Zero) throw new FileProcessingException("无法分配 PDF 预览图像。");
                    try
                    {
                        fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, pixelWidth, pixelHeight, 0xFFFFFFFF);
                        fpdfview.FPDF_RenderPageBitmap(bitmap, page, 0, 0, pixelWidth, pixelHeight, 0, 1);
                        token.ThrowIfCancellationRequested();
                        var stride = fpdfview.FPDFBitmapGetStride(bitmap);
                        var raw = new byte[checked(pixelWidth * pixelHeight * 4)];
                        var pointer = fpdfview.FPDFBitmapGetBuffer(bitmap);
                        for (var row = 0; row < pixelHeight; row++)
                            Marshal.Copy(pointer + row * stride, raw, row * pixelWidth * 4, pixelWidth * 4);
                        return new MagickImage(raw, new PixelReadSettings((uint)pixelWidth, (uint)pixelHeight, StorageType.Char, PixelMapping.BGRA));
                    }
                    finally { fpdfview.FPDFBitmapDestroy(bitmap); }
                }
                finally { fpdfview.FPDF_ClosePage(page); }
            }
            finally { fpdfview.FPDF_CloseDocument(document); }
        }
    }
}
