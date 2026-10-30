using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FileAssistant.Core.Features.Search;
using static FileAssistant.Infrastructure.Windows.EverythingNative;

namespace FileAssistant.Infrastructure.Windows;

/// <summary>Read-only QUERY2W IPC (Everything 1.4.1+). Engine lifecycle is owned by EverythingRuntime.</summary>
public sealed class EverythingSearchClient(string? instance = null)
{
    private const uint ReplyId = 0xFA01;
    private const uint Fields = 0x4 | 0x10 | 0x40; // Full path, size, modified; official SDK IPC header.

    public Task<SearchPage> SearchAsync(SearchRequest request, CancellationToken token) => Task.Run(() => Query(request, token), token);

    private SearchPage Query(SearchRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var expression = SearchRules.Compile(request).EverythingExpression;
        var target = FindWindowW(instance is null ? "EVERYTHING_TASKBAR_NOTIFICATION" : $"EVERYTHING_TASKBAR_NOTIFICATION_({instance})", null);
        if (target == 0 && instance is null) target = FindWindowW("EVERYTHING_TASKBAR_NOTIFICATION_(1.5a)", null);
        if (target == 0) throw new SearchException("内置索引连接已断开。请点击“重试搜索”重新连接，或暂用“指定目录”。");
        if (SendMessageTimeoutW(target, 0x400, 401, 0, 2, 1000, out var loaded) == 0)
            throw new SearchException("内置索引暂时没有响应。可稍后重试，或切换到指定目录。");
        if (loaded == 0) throw new SearchException("正在更新文件索引，请稍后点击“重试搜索”。");

        SearchPage? result = null;
        Exception? error = null;
        nint window = 0, queryBuffer = 0, copyBuffer = 0;
        var module = GetModuleHandleW(null);
        var className = "FileAssistant.Search." + Guid.NewGuid().ToString("N");
        WindowProcedure procedure = (hwnd, message, wParam, lParam) =>
        {
            if (message != CopyDataMessage || wParam != target) return DefWindowProcW(hwnd, message, wParam, lParam);
            try
            {
                var data = Marshal.PtrToStructure<CopyData>(lParam);
                if (data.Identifier != ReplyId) return 0;
                if (data.Length is < 20 or > 16_777_216) throw new InvalidDataException("Invalid Everything response size.");
                var bytes = new byte[data.Length];
                Marshal.Copy(data.Data, bytes, 0, bytes.Length);
                result = ParseReply(bytes, request.PageSize);
            }
            catch (Exception e) { error = e; }
            return 1;
        };
        var cls = new WindowClass { ClassName = className, Procedure = procedure, Instance = module };
        if (RegisterClassW(ref cls) == 0) throw new SearchException("无法创建搜索连接，请重启应用。");
        try
        {
            window = CreateWindowExW(0, className, string.Empty, 0, 0, 0, 0, 0, new nint(-3), 0, module, 0);
            if (window == 0) throw new SearchException("无法创建搜索连接，请重启应用。");
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(bytes, Encoding.Unicode, true))
            {
                writer.Write(unchecked((uint)window.ToInt64()));
                writer.Write(ReplyId);
                writer.Write(0u);
                writer.Write((uint)request.Offset);
                writer.Write((uint)request.PageSize);
                writer.Write(Fields);
                writer.Write(SortCode(request));
                writer.Write(Encoding.Unicode.GetBytes(expression + '\0'));
            }
            var payload = bytes.ToArray();
            queryBuffer = Marshal.AllocHGlobal(payload.Length);
            Marshal.Copy(payload, 0, queryBuffer, payload.Length);
            var copy = new CopyData { Identifier = 18, Length = payload.Length, Data = queryBuffer };
            copyBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<CopyData>());
            Marshal.StructureToPtr(copy, copyBuffer, false);
            if (SendMessageTimeoutW(target, CopyDataMessage, (nuint)window, copyBuffer, 2, 1000, out var accepted) == 0 || accepted == 0)
                throw new SearchException("内置索引未接受查询。请重启本软件后重试。");
            var watch = Stopwatch.StartNew();
            while (result is null && error is null)
            {
                token.ThrowIfCancellationRequested();
                if (watch.Elapsed > TimeSpan.FromSeconds(15)) throw new SearchException("搜索等待超过 15 秒。请缩小范围、改按名称排序，或稍后重试。");
                while (PeekMessageW(out var message, 0, 0, 0, 1)) DispatchMessageW(ref message);
                if (result is null && error is null) MsgWaitForMultipleObjectsEx(0, 0, 50, 0x4FF, 4);
            }
            token.ThrowIfCancellationRequested();
            if (error is not null) throw new SearchException("无法读取索引返回的结果。请重启本软件后重试。");
            return result!;
        }
        finally
        {
            if (window != 0) DestroyWindow(window);
            if (queryBuffer != 0) Marshal.FreeHGlobal(queryBuffer);
            if (copyBuffer != 0) Marshal.FreeHGlobal(copyBuffer);
            UnregisterClassW(className, module);
            GC.KeepAlive(procedure);
        }
    }

    private static uint SortCode(SearchRequest r) => (r.Sort switch { SearchSort.Path => 3u, SearchSort.Size => 5u, SearchSort.Modified => 13u, _ => 1u }) + (r.Descending ? 1u : 0u);

    private static SearchPage ParseReply(byte[] payload, int maximum)
    {
        using var stream = new MemoryStream(payload, false);
        using var reader = new BinaryReader(stream, Encoding.Unicode);
        var total = reader.ReadUInt32();
        var count = reader.ReadUInt32();
        reader.ReadUInt32();
        var flags = reader.ReadUInt32();
        reader.ReadUInt32();
        if (count > maximum || 20L + count * 8L > payload.Length || (flags & 4) == 0 || (flags & ~Fields) != 0) throw new InvalidDataException();
        var items = new List<SearchFile>((int)count);
        for (var i = 0; i < count; i++)
        {
            stream.Position = 20 + i * 8;
            var attributes = reader.ReadUInt32();
            var offset = reader.ReadUInt32();
            if (offset < 20 + count * 8 || offset > payload.Length - 4) throw new InvalidDataException();
            stream.Position = offset;
            var characters = reader.ReadUInt32();
            if (characters > 32768 || stream.Position + (characters + 1) * 2 > payload.Length) throw new InvalidDataException();
            var path = Encoding.Unicode.GetString(reader.ReadBytes(checked((int)characters * 2)));
            if (reader.ReadUInt16() != 0) throw new InvalidDataException();
            long? size = (flags & 0x10) != 0 ? reader.ReadInt64() : null;
            var fileTime = (flags & 0x40) != 0 ? reader.ReadInt64() : 0;
            var folder = (attributes & 3) != 0;
            DateTimeOffset? modified = fileTime > 0 ? DateTimeOffset.FromFileTime(fileTime) : null;
            items.Add(new(path, folder, folder || size < 0 ? null : size, modified));
        }
        return new(items, total, "本机文件索引");
    }
}
