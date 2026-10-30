using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FileAssistant.Core.Abstractions;
using FileAssistant.Core.Features.Search;
using Windows.ApplicationModel.DataTransfer;

namespace FileAssistant.Infrastructure.Windows;

public sealed class WindowsFileInteraction : IFileInteraction
{
    public void Open(SearchFile file)
    {
        VerifyExists(file);
        try { Process.Start(new ProcessStartInfo(file.FullPath) { UseShellExecute = true }); }
        catch (Win32Exception e) when (e.NativeErrorCode == 1155) { throw new FileInteractionException("没有可打开此文件的关联程序，请先在 Windows 中设置默认应用。"); }
        catch (Win32Exception) { throw new FileInteractionException("无法打开此项目，请检查文件访问权限或默认应用设置。"); }
    }

    public void Reveal(SearchFile file)
    {
        VerifyExists(file);
        nint item = 0;
        try
        {
            // An absolute PIDL with zero child items opens the parent and selects this item.
            Marshal.ThrowExceptionForHR(SHParseDisplayName(file.FullPath, 0, out item, 0, out _));
            Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(item, 0, 0, 0));
        }
        catch (COMException) { throw new FileInteractionException("无法在文件夹中定位此项目，请检查路径与访问权限。"); }
        finally { if (item != 0) Marshal.FreeCoTaskMem(item); }
    }

    public void CopyText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        try { Clipboard.SetContent(package); Clipboard.Flush(); }
        catch (Exception e) when (e is COMException or UnauthorizedAccessException) { throw new FileInteractionException("剪贴板暂时不可用，请稍后重试。"); }
    }

    private static void VerifyExists(SearchFile file)
    {
        if (!(file.IsFolder ? Directory.Exists(file.FullPath) : File.Exists(file.FullPath)))
            throw new FileInteractionException("此项目已移动、删除或无法访问，请刷新搜索结果。");
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHParseDisplayName(string name, nint bindContext, out nint item, uint attributes, out uint foundAttributes);
    [DllImport("shell32.dll")] private static extern int SHOpenFolderAndSelectItems(nint item, uint count, nint children, uint flags);
}
