namespace FileAssistant.Core.Features.Organize;

public static class CopyPlanner
{
    public static CopyPlan Create(CopyRequest request, IReadOnlyList<CopySource> sources, Func<string, CopyTarget?> target,
        CancellationToken token = default)
    {
        var destination = Normalize(request.Destination);
        var items = new List<CopyPlanItem>();
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sourcePaths = sources.Select(x => x.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceIdentities = sources.Where(x => x.Stamp is not null).Select(x => x.Stamp!.Identity).ToHashSet(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            token.ThrowIfCancellationRequested();
            var path = destination;
            try
            {
                if (request.Layout == CopyLayout.Preserve && source.Root.Length > 0)
                {
                    if (!roots.TryGetValue(source.Root, out var root))
                    {
                        var rootName = Path.GetFileName(source.Root);
                        if (rootName.Length == 0) rootName = source.Root.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Last().TrimEnd(':') + "_根目录";
                        root = Path.Combine(destination, rootName);
                        if (request.Conflict == CopyConflictPolicy.Number)
                            root = Number(root, true, p => reserved.Contains(p) || Occupied(p, target));
                        roots.Add(source.Root, root);
                        reserved.Add(root);
                    }
                    path = source.Relative.Length == 0 ? root : Path.Combine(root, source.Relative);
                }
                else
                {
                    if (source.IsDirectory)
                    { if (source.Error is not null) items.Add(new(Guid.NewGuid(), source, path, CopyAction.Rejected, source.Error)); continue; }
                    var folder = request.Layout == CopyLayout.Extension ? ExtensionFolder(source.Path) : "";
                    path = Path.Combine(destination, folder, Path.GetFileName(source.Path));
                }
                if (!Within(path, destination)) throw new ArgumentException("目标路径超出所选目标目录。");
                if (source.Error is not null) { items.Add(new(Guid.NewGuid(), source, path, CopyAction.Rejected, source.Error)); continue; }
                var existing = target(path);
                if (existing?.Error is { } error) { items.Add(new(Guid.NewGuid(), source, path, CopyAction.Rejected, error)); continue; }
                if (!source.IsDirectory && (Equal(source.Path, path) || existing?.Stamp?.Identity == source.Stamp?.Identity))
                { items.Add(new(Guid.NewGuid(), source, path, CopyAction.Skip, "源和目标是同一个文件。")); continue; }
                // No policy is allowed to replace another input file in this batch.
                if (!source.IsDirectory && (sourcePaths.Contains(path) || existing?.Stamp is { } stamp && sourceIdentities.Contains(stamp.Identity)))
                { items.Add(new(Guid.NewGuid(), source, path, CopyAction.Rejected, "目标是本批次的其他源文件，请调整位置。")); continue; }
                var blocked = BlockedParent(path, destination, target);
                if (blocked is not null) { items.Add(new(Guid.NewGuid(), source, path, CopyAction.Rejected, blocked)); continue; }
                if (source.IsDirectory)
                {
                    items.Add(new(Guid.NewGuid(), source, path, existing is { IsDirectory: false } ? CopyAction.Rejected : CopyAction.Copy,
                        existing is { IsDirectory: false } ? "目录位置已被文件占用。" : null));
                    reserved.Add(path);
                    continue;
                }
                var batch = reserved.Contains(path);
                var action = CopyAction.Copy;
                string? message = null;
                if (existing is not null || batch)
                {
                    if (request.Conflict == CopyConflictPolicy.Number)
                    { path = Number(path, false, p => reserved.Contains(p) || Occupied(p, target)); action = CopyAction.Number; }
                    else if (request.Conflict == CopyConflictPolicy.Skip) action = CopyAction.Skip;
                    else if (existing?.IsDirectory == true) { action = CopyAction.Pending; message = "目标是目录，请编号保留或跳过。"; }
                    else { action = CopyAction.Pending; message = batch ? "本批次有相同目标，请编号保留或跳过。" : "确认后替换目标，旧文件将保留备份。"; }
                }
                items.Add(new(Guid.NewGuid(), source, path, action, message, existing?.Stamp, batch));
                if (action != CopyAction.Skip) reserved.Add(path);
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or IOException)
            { items.Add(new(Guid.NewGuid(), source, path, CopyAction.Rejected, error.Message)); }
        }
        return new(request with { Destination = destination, Paths = request.Paths.ToArray() }, items.ToArray());
    }

    public static CopyPlan Decide(CopyPlan plan, Guid id, CopyDecision decision, Func<string, CopyTarget?> target)
    {
        var item = plan.Items.Single(x => x.Id == id);
        if (!item.NeedsDecision) throw new InvalidOperationException("此项不需要确认。");
        var replacement = decision switch
        {
            CopyDecision.Skip => item with { Action = CopyAction.Skip, Message = "用户选择跳过。" },
            CopyDecision.Overwrite when item.CanOverwrite => item with { Action = CopyAction.Overwrite, Message = "已确认覆盖；执行前将再次检查目标。" },
            CopyDecision.Number => item with { Action = CopyAction.Number, ApprovedTarget = null, BatchConflict = false,
                Target = Number(item.Target, false, p => Occupied(p, target) || plan.Items.Any(x => x.Id != id && x.Action != CopyAction.Skip && Equal(x.Target, p))), Message = "编号保留两个文件。" },
            _ => throw new InvalidOperationException("本批重名或目录不能直接覆盖，请编号或跳过。")
        };
        return plan with { Items = plan.Items.Select(x => x.Id == id ? replacement : x).ToArray() };
    }

    public static string ExtensionFolder(string path)
    { var extension = Path.GetExtension(path).TrimStart('.'); return extension.Length == 0 ? "无扩展名" : extension.ToUpperInvariant(); }
    public static string Number(string path, bool directory, Func<string, bool> exists)
    {
        if (!exists(path)) return path;
        var parent = Path.GetDirectoryName(path)!;
        var stem = directory ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path);
        var extension = directory ? "" : Path.GetExtension(path);
        for (var index = 1; ; index++)
        {
            var name = $"{stem} ({index}){extension}";
            if (name.Length > 255) throw new PathTooLongException("编号后的文件名超过 255 个字符，请缩短原文件名或使用其他规则。");
            var next = Path.Combine(parent, name); if (!exists(next)) return next;
        }
    }
    private static bool Occupied(string path, Func<string, CopyTarget?> target)
    { var state = target(path); if (state?.Error is { } error) throw new IOException(error); return state is not null; }
    public static bool Equal(string a, string b) => string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
    public static bool Within(string path, string parent) => Equal(path, parent) || Normalize(path).StartsWith(Path.TrimEndingDirectorySeparator(Normalize(parent)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw new ArgumentException("请填写完整的磁盘或网络目录路径。");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        foreach (var segment in full[(Path.GetPathRoot(full)?.Length ?? 0)..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            if (segment.Length > 255 || segment.EndsWith('.') || segment.EndsWith(' ') || segment.Contains(':')) throw new ArgumentException("路径包含过长的单个名称、不支持的名称或数据流。");
        return full;
    }
    private static string? BlockedParent(string path, string destination, Func<string, CopyTarget?> target)
    {
        for (var parent = Path.GetDirectoryName(path); parent is not null && Within(parent, destination); parent = Path.GetDirectoryName(parent))
        { var state = target(parent); if (state?.Error is { } error) return error; if (state is { IsDirectory: false }) return "上级目录位置已被文件占用。"; }
        return null;
    }
}
