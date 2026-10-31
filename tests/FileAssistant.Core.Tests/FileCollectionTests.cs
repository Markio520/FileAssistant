using FileAssistant.Core.Features.FileCollections;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.Core.Tests;

public sealed class FileCollectionTests
{
    private static SearchFile File(string name) => new(@"C:\fixtures\" + name, false, 10, null);
    [Fact]
    public void Memberships_are_unique_per_folder_and_execution_expands_and_deduplicates_them()
    {
        var collection = new FileCollection(); var first = collection.CreateFolder(default, "图片"); var child = collection.CreateFolder(first, "子目录");
        Assert.Equal(1, collection.AddFiles(first, [File("one.png"), File("ONE.png")]));
        Assert.Equal(2, collection.AddFiles(child, [File("one.png"), File("two.webp")]));
        Assert.Equal(3, collection.Entries.Count); Assert.Equal(2, collection.Resolve(first).Count);
        Assert.Equal(2, collection.ResolveSelection([first, child], collection.Entries.Select(entry => entry.Id)).Count);
        Assert.Equal("图片 / 子目录", collection.FolderPath(child));
    }
    [Fact]
    public void Moving_and_dissolving_folders_preserve_memberships_and_reject_cycles()
    {
        var collection = new FileCollection(); var first = collection.CreateFolder(default, "A"); var second = collection.CreateFolder(first, "B");
        collection.AddFiles(first, [File("a.png")]); collection.AddFiles(second, [File("b.png")]);
        Assert.Throws<ArgumentException>(() => collection.Move([first], [], second));
        collection.Dissolve(first); Assert.Equal(Guid.Empty, collection.Parent(second));
        Assert.Equal(2, collection.Resolve(default).Count);
        collection.ClearFiles(default); Assert.Empty(collection.Entries); Assert.Single(collection.Folders);
    }
    [Fact]
    public void Transfers_keep_structure_but_are_independent_and_flatten_without_duplicate_paths()
    {
        var source = new FileCollection(); var group = source.CreateFolder(default, "材料"); var child = source.CreateFolder(group, "图片");
        source.AddFiles(group, [File("a.png")]); source.AddFiles(child, [File("a.png"), File("b.png")]);
        var bundle = source.Export(default, [group, child], source.Entries.Select(entry => entry.Id));
        var target = new FileCollection(); var destination = target.CreateFolder(default, "转换");
        Assert.Equal(3, target.Import(destination, bundle, true));
        Assert.Equal(2, target.Resolve(destination).Count);
        source.Remove([group], []); Assert.Empty(source.Entries); Assert.Equal(3, target.Entries.Count);
        var flat = new FileCollection(); Assert.Equal(2, flat.Import(default, bundle, false)); Assert.Empty(flat.Folders);
    }
    [Fact]
    public void File_only_transfer_across_groups_is_flattened_once_and_same_level_names_are_numbered()
    {
        var source = new FileCollection(); var a = source.CreateFolder(default, "图片"); var b = source.CreateFolder(default, "图片");
        Assert.Equal("图片 (2)", source.Folders.Single(folder => folder.Id == b).Name);
        source.AddFiles(a, [File("same.png")]); source.AddFiles(b, [File("same.png")]);
        var bundle = source.Export(default, [], source.Entries.Select(entry => entry.Id));
        Assert.Single(bundle.Entries); var target = new FileCollection(); Assert.Equal(1, target.Import(default, bundle, true));
        Assert.Throws<ArgumentException>(() => source.RenameFolder(b, "图片"));
    }
    [Fact]
    public void Restore_rejects_dangling_parents_cycles_and_duplicate_memberships()
    {
        var id = Guid.NewGuid(); var other = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => FileCollection.Restore(new(1, [new(id, other, "坏目录")], [])));
        Assert.Throws<ArgumentException>(() => FileCollection.Restore(new(1, [new(id, id, "循环")], [])));
        Assert.Throws<ArgumentException>(() => FileCollection.Restore(new(1, [], [new(id, default, File("a.png")), new(other, default, File("A.png"))])));
    }
}
