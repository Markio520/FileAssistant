using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using FileAssistant.Core.Features.Organize;

namespace FileAssistant.Infrastructure.FileSystem;

internal static class WindowsCopyFile
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int ProgressCallback(nint message, nint context);
    [StructLayout(LayoutKind.Sequential)] private struct Parameters
    { public uint Size, Flags; public nint Cancel; public ProgressCallback Callback; public nint Context; }
    [StructLayout(LayoutKind.Sequential)] private struct FileInformation
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh,
            Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int CopyFile2(string source, string target, ref Parameters parameters);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint creation, uint flags, nint template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, nint information, uint size);

    public static void Copy(string source, string target, Action<long> progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Exception? callbackError = null;
        ProgressCallback callback = (message, _) =>
        {
            if (token.IsCancellationRequested) return 1; // CANCEL deletes partial data; STOP would retain it.
            try
            {
                if (Marshal.ReadInt32(message) == 2)
                    progress(Marshal.ReadInt64(message, 8 + 8 + 2 * IntPtr.Size + 5 * sizeof(long)));
            }
            catch (Exception error) { callbackError = error; return 1; }
            return 0;
        };
        var parameters = new Parameters { Size = (uint)Marshal.SizeOf<Parameters>(), Flags = 1, Callback = callback };
        var result = CopyFile2(Extended(source), Extended(target), ref parameters);
        GC.KeepAlive(callback);
        if (callbackError is not null) throw new IOException("无法更新复制进度。", callbackError);
        if (result < 0)
        {
            token.ThrowIfCancellationRequested();
            throw new IOException(new Win32Exception(result & 0xffff).Message, result);
        }
        // A cancellation arriving after native completion is handled before commit by the caller.
    }
    public static FileStamp Stamp(FileStream stream)
    {
        if (!GetFileInformationByHandle(stream.SafeFileHandle, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(((long)info.SizeHigh << 32) | info.SizeLow,
            DateTime.FromFileTimeUtc(((long)info.WriteHigh << 32) | info.WriteLow).Ticks,
            $"{info.Volume:X8}:{info.IndexHigh:X8}{info.IndexLow:X8}");
    }
    public static FileStream LockForRename(string path)
    {
        var handle = CreateFileW(Extended(path), 0x80010000, 1, 0, 3, 0, 0); // READ + DELETE, share READ only.
        if (handle.IsInvalid) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new IOException(new Win32Exception(error).Message, unchecked((int)(0x80070000u | (uint)error))); }
        return new FileStream(handle, FileAccess.Read);
    }
    public static void RenameLocked(FileStream stream, string target)
    {
        var name = System.Text.Encoding.Unicode.GetBytes(Extended(Path.GetFullPath(target)));
        var offset = IntPtr.Size == 8 ? 20 : 12;
        var size = offset + name.Length + sizeof(char);
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            for (var i = 0; i < offset; i++) Marshal.WriteByte(buffer, i, 0);
            Marshal.WriteInt32(buffer, offset - 4, name.Length);
            Marshal.Copy(name, 0, buffer + offset, name.Length);
            Marshal.WriteInt16(buffer, offset + name.Length, 0);
            if (!SetFileInformationByHandle(stream.SafeFileHandle, 3, buffer, (uint)size))
            { var error = Marshal.GetLastWin32Error(); throw new IOException(new Win32Exception(error).Message, unchecked((int)(0x80070000u | (uint)error))); }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    private static string Extended(string path) => path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path
        : path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path[2..] : @"\\?\" + path;
}
