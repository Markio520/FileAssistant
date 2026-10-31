using FileAssistant.Core.Features.Search;

namespace FileAssistant.Core.Features.FileCollections;

/// <summary>Software folders and memberships. All operations change references only and perform no file I/O.</summary>
public sealed class FileCollection
{
    private readonly List<CollectionFolder> folders = [];
    private readonly List<CollectionEntry> entries = [];
    public IReadOnlyList<CollectionFolder> Folders => folders;
    public IReadOnlyList<CollectionEntry> Entries => entries;
    public bool HasFolder(Guid id) => id == Guid.Empty || folders.Any(folder => folder.Id == id);
    public FileCollectionSnapshot Snapshot(Guid current = default) => new(1, folders.ToArray(), entries.ToArray(), current);

    public static FileCollection Restore(FileCollectionSnapshot snapshot)
    {
        if (snapshot.Version != 1) throw new ArgumentException("文件夹数据版本不受支持。");
        var result = new FileCollection();
        if (snapshot.Folders is null || snapshot.Entries is null) throw new ArgumentException("文件夹数据不完整。");
        result.folders.AddRange(snapshot.Folders); result.entries.AddRange(snapshot.Entries);
        if (result.folders.Any(folder => folder.Id == Guid.Empty || !result.HasFolder(folder.ParentId) || !ValidName(folder.Name)) ||
            result.folders.Select(folder => folder.Id).Distinct().Count() != result.folders.Count ||
            result.entries.Select(entry => entry.Id).Distinct().Count() != result.entries.Count ||
            result.entries.Any(entry => entry.Id == Guid.Empty || !result.HasFolder(entry.FolderId) || entry.File is null || !Path.IsPathFullyQualified(entry.File.FullPath)))
            throw new ArgumentException("文件夹数据包含无效条目。");
        foreach (var folder in result.folders)
        {
            var seen = new HashSet<Guid> { folder.Id }; var parent = folder.ParentId;
            while (parent != Guid.Empty)
            {
                if (!seen.Add(parent) || seen.Count > 128) throw new ArgumentException("文件夹层级无效或过深。");
                parent = result.folders.First(item => item.Id == parent).ParentId;
            }
        }
        if (result.folders.GroupBy(folder => (folder.ParentId, Name: folder.Name.ToUpperInvariant())).Any(group => group.Count() > 1) ||
            result.entries.GroupBy(entry => (entry.FolderId, Path: Normalize(entry.File.FullPath).ToUpperInvariant())).Any(group => group.Count() > 1))
            throw new ArgumentException("文件夹数据包含重复条目。");
        for (var index = 0; index < result.entries.Count; index++)
            result.entries[index] = result.entries[index] with { File = result.entries[index].File with { FullPath = Normalize(result.entries[index].File.FullPath) } };
        return result;
    }
    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name == name.Trim() && name.Length <= 120 &&
        name is not ("." or "..") && !name.Any(character => char.IsControl(character) || character is '/' or '\\');
    private void RequireFolder(Guid id) { if (!HasFolder(id)) throw new ArgumentException("目标软件文件夹已不存在。"); }
    private static string Normalize(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.Contains("://", StringComparison.Ordinal)) throw new ArgumentException("文件引用需要完整的磁盘路径。");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
    public string UniqueName(Guid parent, string name, Guid except = default)
    {
        var candidate = name;
        for (var suffix = 2; folders.Any(folder => folder.ParentId == parent && folder.Id != except && folder.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)); suffix++)
            candidate = name[..Math.Min(name.Length, 110)] + $" ({suffix})";
        return candidate;
    }
    public Guid CreateFolder(Guid parent, string name)
    {
        RequireFolder(parent); name = name.Trim();
        if (Ancestors(parent).Count >= 128) throw new ArgumentException("软件文件夹层级过深。");
        if (!ValidName(name)) throw new ArgumentException("名称应为 1–120 个字符，不能包含斜杠、换行或仅为点。");
        var id = Guid.NewGuid(); folders.Add(new(id, parent, UniqueName(parent, name))); return id;
    }
    public void RenameFolder(Guid id, string name)
    {
        var folder = folders.FirstOrDefault(item => item.Id == id) ?? throw new ArgumentException("软件文件夹已不存在。");
        name = name.Trim();
        if (!ValidName(name)) throw new ArgumentException("软件文件夹名称无效。");
        if (folders.Any(item => item.Id != id && item.ParentId == folder.ParentId && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("同级已有这个名称，请换一个名称。");
        folders[folders.IndexOf(folder)] = folder with { Name = name };
    }
    public int AddFiles(Guid folder, IEnumerable<SearchFile> files)
    {
        RequireFolder(folder);
        var known = entries.Where(entry => entry.FolderId == folder).Select(entry => Normalize(entry.File.FullPath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var file in files)
            if (known.Add(Normalize(file.FullPath))) { entries.Add(new(Guid.NewGuid(), folder, file with { FullPath = Normalize(file.FullPath) })); added++; }
        return added;
    }
    public IReadOnlyList<Guid> Ancestors(Guid id)
    {
        RequireFolder(id); var ancestors = new List<Guid>();
        while (id != Guid.Empty) { ancestors.Add(id); id = folders.First(folder => folder.Id == id).ParentId; }
        ancestors.Add(Guid.Empty); ancestors.Reverse(); return ancestors;
    }
    public string FolderPath(Guid id) => string.Join(" / ", Ancestors(id).Where(item => item != Guid.Empty).Select(item => folders.First(folder => folder.Id == item).Name));
    public Guid Parent(Guid id) => id == Guid.Empty ? Guid.Empty : folders.First(folder => folder.Id == id).ParentId;
    private HashSet<Guid> Descendants(Guid id)
    {
        RequireFolder(id); var result = new HashSet<Guid> { id }; var pending = new Queue<Guid>(); pending.Enqueue(id);
        while (pending.TryDequeue(out var parent))
            foreach (var folder in folders.Where(folder => folder.ParentId == parent)) if (result.Add(folder.Id)) pending.Enqueue(folder.Id);
        return result;
    }
    public IReadOnlyList<CollectionEntry> Resolve(Guid folder)
    {
        RequireFolder(folder);
        var result = new List<CollectionEntry>();
        void Visit(Guid id)
        {
            foreach (var child in folders.Where(item => item.ParentId == id)) Visit(child.Id);
            result.AddRange(entries.Where(entry => entry.FolderId == id));
        }
        Visit(folder);
        return result.DistinctBy(entry => entry.File.FullPath, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public IReadOnlyList<CollectionEntry> ResolveSelection(IEnumerable<Guid> folderIds, IEnumerable<Guid> entryIds)
    {
        var groups = folderIds.ToHashSet(); var selectedEntries = entryIds.ToHashSet();
        var selectedFolders = new HashSet<Guid>();
        foreach (var id in groups) selectedFolders.UnionWith(Descendants(id));
        return ResolveOrdered().Where(entry => selectedFolders.Contains(entry.FolderId) || selectedEntries.Contains(entry.Id))
            .DistinctBy(entry => entry.File.FullPath, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private IReadOnlyList<CollectionEntry> ResolveOrdered()
    {
        var result = new List<CollectionEntry>();
        void Visit(Guid id) { foreach (var child in folders.Where(folder => folder.ParentId == id)) Visit(child.Id); result.AddRange(entries.Where(entry => entry.FolderId == id)); }
        Visit(Guid.Empty); return result;
    }
    public FileCollectionBundle Export(Guid current, IEnumerable<Guid>? folderIds = null, IEnumerable<Guid>? entryIds = null)
    {
        RequireFolder(current);
        var selected = (folderIds ?? folders.Where(folder => folder.ParentId == current).Select(folder => folder.Id)).ToHashSet();
        selected.RemoveWhere(id => !HasFolder(id) || id == Guid.Empty);
        var roots = selected.Where(id => !Ancestors(id).Any(ancestor => ancestor != id && selected.Contains(ancestor))).ToHashSet();
        var included = new HashSet<Guid>(); foreach (var id in roots) included.UnionWith(Descendants(id));
        var direct = (entryIds ?? entries.Where(entry => entry.FolderId == current).Select(entry => entry.Id)).ToHashSet();
        return new(folders.Where(folder => included.Contains(folder.Id)).Select(folder => roots.Contains(folder.Id) ? folder with { ParentId = Guid.Empty } : folder).ToArray(),
            ResolveOrdered().Where(entry => included.Contains(entry.FolderId) || direct.Contains(entry.Id))
                .Select(entry => included.Contains(entry.FolderId) ? entry : entry with { FolderId = Guid.Empty })
                .DistinctBy(entry => (entry.FolderId, Path: Normalize(entry.File.FullPath).ToUpperInvariant())).ToArray());
    }
    public int Import(Guid destination, FileCollectionBundle bundle, bool preserveFolders)
    {
        RequireFolder(destination);
        // Validate the portable tree before applying any changes.
        var source = Restore(new(1, bundle.Folders, bundle.Entries));
        if (!preserveFolders) return AddFiles(destination, bundle.Files);
        if (source.Folders.Any(folder => Ancestors(destination).Count + source.Ancestors(folder.Id).Count - 1 > 128))
            throw new ArgumentException("导入后的软件文件夹层级过深。");
        var map = new Dictionary<Guid, Guid> { [Guid.Empty] = destination };
        var pending = bundle.Folders.ToList();
        while (pending.Count > 0)
            foreach (var folder in pending.ToArray())
                if (map.TryGetValue(folder.ParentId, out var parent)) { map.Add(folder.Id, CreateFolder(parent, folder.Name)); pending.Remove(folder); }
        var added = 0;
        foreach (var group in bundle.Entries.GroupBy(entry => map[entry.FolderId])) added += AddFiles(group.Key, group.Select(entry => entry.File));
        return added;
    }
    public bool CanMove(IEnumerable<Guid> folderIds, Guid destination) => HasFolder(destination) && folderIds.All(id => id != Guid.Empty && HasFolder(id) && !Descendants(id).Contains(destination));
    public void Move(IEnumerable<Guid> folderIds, IEnumerable<Guid> entryIds, Guid destination)
    {
        var ids = folderIds.ToHashSet(); var files = entryIds.ToHashSet();
        if (!CanMove(ids, destination)) throw new ArgumentException("不能把软件文件夹移到自身或其子文件夹中。");
        var roots = ids.Where(id => !Ancestors(id).Any(ancestor => ancestor != id && ids.Contains(ancestor))).ToArray();
        foreach (var root in roots)
            if (Descendants(root).Any(id => Ancestors(destination).Count + Ancestors(id).Count - Ancestors(root).Count >= 128))
                throw new ArgumentException("移动后的软件文件夹层级过深。");
        var movedTree = new HashSet<Guid>(); foreach (var id in roots) movedTree.UnionWith(Descendants(id));
        foreach (var id in roots)
        {
            var index = folders.FindIndex(folder => folder.Id == id); var folder = folders[index];
            folders[index] = folder with { ParentId = destination, Name = UniqueName(destination, folder.Name, id) };
        }
        var memberships = entries.Where(entry => entry.FolderId == destination).Select(entry => Normalize(entry.File.FullPath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.Where(entry => files.Contains(entry.Id) && !movedTree.Contains(entry.FolderId) && entry.FolderId != destination).ToArray())
        {
            if (memberships.Add(Normalize(entry.File.FullPath))) entries[entries.IndexOf(entry)] = entry with { FolderId = destination };
            else entries.Remove(entry);
        }
    }
    public void Remove(IEnumerable<Guid> folderIds, IEnumerable<Guid> entryIds)
    {
        var removed = new HashSet<Guid>(); foreach (var id in folderIds.Where(id => id != Guid.Empty)) removed.UnionWith(Descendants(id));
        var files = entryIds.ToHashSet();
        entries.RemoveAll(entry => removed.Contains(entry.FolderId) || files.Contains(entry.Id)); folders.RemoveAll(folder => removed.Contains(folder.Id));
    }
    public void Dissolve(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("根目录不能解散。");
        var parent = Parent(id);
        Move(folders.Where(folder => folder.ParentId == id).Select(folder => folder.Id).ToArray(), entries.Where(entry => entry.FolderId == id).Select(entry => entry.Id).ToArray(), parent);
        folders.RemoveAll(folder => folder.Id == id);
    }
    public void ClearFiles(Guid id) { var descendants = Descendants(id); entries.RemoveAll(entry => descendants.Contains(entry.FolderId)); }
    public void UpdateReferences(IReadOnlyDictionary<string, (SearchFile File, string? Error)> updates)
    {
        for (var index = 0; index < entries.Count; index++)
            if (updates.TryGetValue(entries[index].File.FullPath, out var update)) entries[index] = entries[index] with { File = update.File, Error = update.Error };
    }
}
