using FileAssistant.App.Features.Convert;
using FileAssistant.App.Features.Outputs;
using FileAssistant.App.Features.Tasks;
using FileAssistant.App.Features.Workspace;
using FileAssistant.App.Features.FileInput;
using FileAssistant.Core.Abstractions;
using FileAssistant.Core.Engines;
using FileAssistant.Core.Features.FileCollections;
using FileAssistant.Core.Features.FileInput;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Tests;

public sealed class FileCollectionWorkflowTests
{
    private static SearchFile File(string name) => new(@"C:\fixtures\" + name, false, 10, null);
    [Fact]
    public async Task Conversion_uses_the_current_folder_and_selected_folders_expand_without_leaking_other_groups()
    {
        var input = new WorkspaceViewModel(new Reader(), WorkspaceInputPurpose.Conversion);
        var images = input.CreateFolder("图片"); var documents = input.CreateFolder("文档"); var empty = input.CreateFolder("空目录");
        input.EnterFolder(images); input.AddSearchFiles([File("one.png"), File("two.webp")]);
        input.EnterFolder(documents); input.AddSearchFiles([File("notes.txt")]); input.EnterFolder(default);
        var processing = new Processing();
        var vm = new ConversionViewModel(processing, input, new(new Interaction()), new());
        Assert.Empty(vm.Formats);
        input.EnterFolder(images);
        Assert.True(vm.CanBuildPlan); Assert.Contains("图片", vm.ScopeSummary);
        await vm.BuildPlanCommand.ExecuteAsync(null);
        Assert.Equal([File("one.png").FullPath, File("two.webp").FullPath], processing.Request!.Paths);
        input.EnterFolder(default); Assert.Null(vm.Plan);
        input.SetSelection([input.VisibleFiles.Single(row => row.FolderId == images)]);
        Assert.Equal(2, input.EffectiveEntries.Count); Assert.True(vm.CanBuildPlan);
        input.SetSelection([input.VisibleFiles.Single(row => row.FolderId == empty)]);
        Assert.Empty(input.EffectiveEntries); Assert.False(vm.CanBuildPlan);
    }
    [Fact]
    public void Groups_can_share_a_path_and_clear_only_affects_the_current_subtree()
    {
        var vm = new WorkspaceViewModel(); var a = vm.CreateFolder("A"); var b = vm.CreateFolder("B");
        vm.EnterFolder(a); var child = vm.CreateFolder("child"); vm.AddSearchFiles([File("same.png")]);
        vm.EnterFolder(child); vm.AddSearchFiles([File("one.png")]);
        vm.EnterFolder(b); vm.AddSearchFiles([File("same.png"), File("two.png")]); vm.EnterFolder(default);
        Assert.Equal(3, vm.SearchEntries.Count);
        vm.SetSelection(vm.VisibleFiles.ToArray()); Assert.Equal(3, vm.SelectedEntries.Count);
        vm.EnterFolder(a); vm.ClearCommand.Execute(null);
        Assert.Empty(vm.ScopeEntries); Assert.Equal(2, vm.SearchEntries.Count);
        Assert.Equal(3, vm.CollectionSnapshot.Folders.Count);
        vm.EnterFolder(child); Assert.True(vm.ShowEmpty); Assert.False(vm.ShowNoResults);
    }
    [Fact]
    public async Task Structured_transfer_revalidates_files_and_leaves_source_and_destination_independent()
    {
        var source = new WorkspaceViewModel(); var folder = source.CreateFolder("资料"); source.EnterFolder(folder);
        source.AddSearchFiles([File("good.png"), File("gone.png")]); source.EnterFolder(default);
        var bundle = source.ExportCurrentFolder();
        var target = new WorkspaceViewModel(new Reader { Missing = File("gone.png").FullPath }, WorkspaceInputPurpose.Copy);
        var destination = target.CreateFolder("工作");
        var report = await target.ImportBundleAsync(bundle, destination, true);
        Assert.Equal(1, report.Unavailable); Assert.Single(target.SearchEntries);
        Assert.Equal(2, target.CollectionSnapshot.Folders.Count);
        source.RemoveFromList(source.VisibleFiles.ToArray()); Assert.Empty(source.SearchEntries);
        Assert.Single(target.SearchEntries); Assert.Equal(destination, target.CurrentFolderId);
    }
    [Fact]
    public async Task Auto_save_flushes_the_latest_snapshot_and_restores_the_current_folder_without_selection()
    {
        var store = new Store(); var vm = new WorkspaceViewModel(new Reader(), WorkspaceInputPurpose.Staging, store);
        await vm.LoadCollectionAsync(); var group = vm.CreateFolder("持久化"); vm.EnterFolder(group); vm.AddSearchFiles([File("one.png")]);
        vm.SetSelection(vm.VisibleFiles.ToArray()); await vm.FlushCollectionAsync();
        Assert.False(vm.HasUnsavedCollection); Assert.Single(store.Saves); Assert.Equal(group, store.Saves[0].CurrentFolderId);
        var restored = new WorkspaceViewModel(new Reader(), WorkspaceInputPurpose.Staging, store);
        await restored.LoadCollectionAsync();
        Assert.Equal(group, restored.CurrentFolderId); Assert.Single(restored.ScopeEntries); Assert.Empty(restored.SelectedEntries);
        Assert.False(restored.IsLoading); Assert.False(restored.HasSamples);
    }
    [Fact]
    public async Task Save_failures_can_be_retried_and_an_unknown_schema_is_not_overwritten()
    {
        var store = new Store { FailSave = true };
        var vm = new WorkspaceViewModel(new Reader(), WorkspaceInputPurpose.Copy, store); await vm.LoadCollectionAsync(); vm.CreateFolder("A");
        await vm.FlushCollectionAsync(); Assert.True(vm.HasUnsavedCollection);
        store.FailSave = false; await vm.FlushCollectionAsync(); Assert.False(vm.HasUnsavedCollection);
        var protectedStore = new Store { ReadOnly = true };
        var protectedVm = new WorkspaceViewModel(new Reader(), WorkspaceInputPurpose.Copy, protectedStore);
        await protectedVm.LoadCollectionAsync(); protectedVm.CreateFolder("临时"); await protectedVm.FlushCollectionAsync();
        Assert.Empty(protectedStore.Saves);
    }
    [Fact]
    public void Moving_references_and_dissolving_a_folder_do_not_change_the_file_path()
    {
        var vm = new WorkspaceViewModel(); vm.AddSearchFiles([File("source.png")]); var folder = vm.CreateFolder("收集");
        var file = vm.VisibleFiles.Single(row => row.Source is not null); vm.SetSelection([file]); vm.MoveSelectionTo(folder);
        Assert.Single(vm.SearchEntries); Assert.DoesNotContain(vm.VisibleFiles, row => row.Source is not null);
        vm.DissolveFolder(folder); Assert.Equal(File("source.png").FullPath, Assert.Single(vm.SearchEntries).FullPath);
        Assert.Empty(vm.CollectionSnapshot.Folders);
    }
    [Fact]
    public async Task Changing_folder_invalidates_a_pending_plan_even_if_both_folders_contain_the_same_path()
    {
        var vm = new WorkspaceViewModel(); var a = vm.CreateFolder("A"); var b = vm.CreateFolder("B");
        vm.EnterFolder(a); vm.AddSearchFiles([File("same.png")]); vm.EnterFolder(b); vm.AddSearchFiles([File("same.png")]); vm.EnterFolder(a);
        var pending = new TaskCompletionSource<ConversionPlan>(); var processing = new Processing { Pending = pending.Task };
        var conversion = new ConversionViewModel(processing, vm, new(new Interaction()), new());
        var building = conversion.BuildPlanCommand.ExecuteAsync(null); vm.EnterFolder(b);
        pending.SetResult(new(processing.Request!, [])); await building;
        Assert.Null(conversion.Plan); Assert.False(conversion.CanRun); Assert.Contains("已变化", conversion.Feedback);
    }
    [Fact]
    public async Task Restoring_saved_references_cannot_be_cancelled_into_an_editable_empty_list()
    {
        var pending = new TaskCompletionSource<CollectionLoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var collection = new FileCollection(); collection.AddFiles(default, [File("saved.png")]);
        var vm = new WorkspaceViewModel(new Reader(), WorkspaceInputPurpose.Staging, new Store { PendingLoad = pending.Task });
        var loading = vm.LoadCollectionAsync();
        Assert.True(vm.IsLoading); Assert.False(vm.CancelLoadingCommand.CanExecute(null));
        vm.CancelLoadingCommand.Execute(null); vm.ClearCommand.Execute(null);
        await vm.ImportPathsAsync([File("new.png").FullPath]);
        Assert.True(vm.IsLoading); Assert.False(vm.CanReceiveFiles);
        pending.SetResult(new(collection.Snapshot())); await loading;
        Assert.Equal(File("saved.png").FullPath, Assert.Single(vm.SearchEntries).FullPath);
        Assert.True(vm.CanReceiveFiles);
    }
    [Fact]
    public async Task Cancelled_transfer_cannot_unlock_or_replace_a_newer_import()
    {
        var reader = new PendingReader(); var vm = new WorkspaceViewModel(reader);
        var first = vm.ImportBundleAsync(FileCollectionBundle.FromFiles([File("old.png")]), default, true);
        vm.CancelLoadingCommand.Execute(null);
        var second = vm.ImportPathsAsync([File("new.png").FullPath]);
        reader.Complete(0, "old.png"); await first;
        Assert.True(vm.IsLoading); Assert.False(vm.CanReceiveFiles); Assert.Empty(vm.SearchEntries);
        reader.Complete(1, "new.png"); await second;
        Assert.Equal(File("new.png").FullPath, Assert.Single(vm.SearchEntries).FullPath);
        Assert.False(vm.IsLoading);
    }
    [Fact]
    public async Task Cancelled_reference_check_leaves_later_edits_and_loading_state_intact()
    {
        var reader = new PendingReader(); var vm = new WorkspaceViewModel(reader); vm.AddSearchFiles([File("old.png")]);
        var checking = vm.RefreshReferencesAsync(); vm.CancelLoadingCommand.Execute(null); vm.ClearCommand.Execute(null);
        var importing = vm.ImportPathsAsync([File("new.png").FullPath]);
        reader.Complete(0, "old.png"); await checking;
        Assert.True(vm.IsLoading); Assert.Empty(vm.SearchEntries);
        reader.Complete(1, "new.png"); await importing;
        Assert.Equal(File("new.png").FullPath, Assert.Single(vm.SearchEntries).FullPath);
        Assert.False(vm.IsLoading);
    }
    [Fact]
    public void Undo_move_restores_memberships_even_when_the_destination_merged_duplicate_paths()
    {
        var vm = new WorkspaceViewModel(); var folder = vm.CreateFolder("目标");
        vm.AddSearchFiles([File("same.png"), File("other.png")]); vm.EnterFolder(folder); vm.AddSearchFiles([File("same.png")]); vm.GoUp();
        var before = vm.CollectionSnapshot;
        vm.MoveItemsTo(vm.VisibleFiles.Where(row => row.EntryId is not null).ToArray(), folder);
        Assert.Equal(2, vm.CollectionSnapshot.Entries.Count); Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Equal(before.Entries, vm.CollectionSnapshot.Entries); Assert.Equal(before.Folders, vm.CollectionSnapshot.Folders);
        Assert.False(vm.CanUndo); Assert.Equal(2, vm.SearchEntries.Count);
    }
    [Fact]
    public void Undo_clear_restores_the_subtree_and_its_original_location_after_navigation()
    {
        var vm = new WorkspaceViewModel(); vm.AddSearchFiles([File("outside.png")]); var folder = vm.CreateFolder("A");
        vm.EnterFolder(folder); vm.AddSearchFiles([File("inside.png")]); var nested = vm.CreateFolder("B");
        vm.EnterFolder(nested); vm.AddSearchFiles([File("nested.png")]); vm.GoUp();
        vm.ClearCommand.Execute(null); Assert.Single(vm.SearchEntries); Assert.Equal(2, vm.CollectionSnapshot.Folders.Count);
        vm.GoUp(); vm.UndoCommand.Execute(null);
        Assert.Equal(folder, vm.CurrentFolderId); Assert.Equal(2, vm.ScopeEntries.Count); Assert.Equal(3, vm.SearchEntries.Count);
    }
    [Fact]
    public void Undo_removed_folder_restores_its_tree_without_changing_another_input()
    {
        var vm = new WorkspaceViewModel(); var folder = vm.CreateFolder("资料"); vm.EnterFolder(folder); vm.CreateFolder("子目录"); vm.AddSearchFiles([File("inside.png")]); vm.GoUp();
        var other = new WorkspaceViewModel(); other.InitializeCollection(vm.CollectionSnapshot); other.AddSearchFiles([File("other.png")]);
        vm.RemoveFromList(vm.VisibleFiles); Assert.Empty(vm.SearchEntries); Assert.Empty(vm.CollectionSnapshot.Folders);
        vm.UndoCommand.Execute(null);
        Assert.Single(vm.SearchEntries); Assert.Equal(2, vm.CollectionSnapshot.Folders.Count); Assert.Equal(2, other.SearchEntries.Count);
    }
    [Fact]
    public void Undo_only_reverts_the_latest_edit_and_a_duplicate_add_does_not_replace_it()
    {
        var vm = new WorkspaceViewModel(); vm.AddSearchFiles([File("one.png")]); vm.CreateFolder("A");
        vm.AddSearchFiles([File("one.png")]); vm.UndoCommand.Execute(null);
        Assert.Empty(vm.CollectionSnapshot.Folders); Assert.Single(vm.SearchEntries); Assert.False(vm.CanUndo);
        vm.AddSearchFiles([File("two.png")]); vm.UndoCommand.Execute(null); vm.UndoCommand.Execute(null);
        Assert.Equal(File("one.png").FullPath, Assert.Single(vm.SearchEntries).FullPath);
    }
    [Fact]
    public async Task Undo_is_saved_but_undo_history_is_not_restored_on_restart()
    {
        var store = new Store(); var vm = new WorkspaceViewModel(new Reader(), WorkspaceInputPurpose.Staging, store);
        await vm.LoadCollectionAsync(); vm.AddSearchFiles([File("one.png")]); await vm.FlushCollectionAsync();
        vm.ClearCommand.Execute(null); await vm.FlushCollectionAsync(); Assert.Empty(store.Saves.Last().Entries);
        vm.UndoCommand.Execute(null); await vm.FlushCollectionAsync(); Assert.Single(store.Saves.Last().Entries);
        var restored = new WorkspaceViewModel(new Reader(), WorkspaceInputPurpose.Staging, store); await restored.LoadCollectionAsync();
        Assert.Single(restored.SearchEntries); Assert.False(restored.HasUndo); Assert.False(restored.HasUnsavedCollection);
    }
    [Fact]
    public async Task Undo_is_blocked_during_import_and_cancelled_results_do_not_replace_history()
    {
        var reader = new PendingReader(); var vm = new WorkspaceViewModel(reader); vm.AddSearchFiles([File("old.png")]);
        var importing = vm.ImportPathsAsync([File("new.png").FullPath]);
        Assert.False(vm.CanUndo); vm.UndoCommand.Execute(null); Assert.Single(vm.SearchEntries);
        vm.CancelLoadingCommand.Execute(null); reader.Complete(0, "new.png"); await importing;
        Assert.True(vm.CanUndo); vm.UndoCommand.Execute(null); Assert.Empty(vm.SearchEntries);
    }
    [Fact]
    public void Source_selection_retains_files_across_folders_and_deselects_only_the_visible_folder()
    {
        var vm = new WorkspaceViewModel(); var a = vm.CreateFolder("A"); var b = vm.CreateFolder("B");
        vm.EnterFolder(a); vm.AddSearchFiles([File("a.png")]); vm.EnterFolder(b); vm.AddSearchFiles([File("b.png")]);
        var selection = new CollectionSourceSelection(vm.CollectionSnapshot);
        vm.EnterFolder(a); selection.UpdateVisible(vm.VisibleFiles, vm.VisibleFiles);
        vm.EnterFolder(b); selection.UpdateVisible(vm.VisibleFiles, vm.VisibleFiles);
        Assert.Equal(2, selection.Bundle.Files.Count); Assert.Equal(2, selection.Count); Assert.Empty(vm.SelectedEntries);
        vm.EnterFolder(a); Assert.True(selection.Contains(vm.VisibleFiles.Single())); selection.UpdateVisible(vm.VisibleFiles, []);
        Assert.Equal(File("b.png").FullPath, Assert.Single(selection.Bundle.Files).FullPath);
    }
    [Fact]
    public void Source_selection_expands_and_deduplicates_folders_but_browsing_does_not_select_the_whole_folder()
    {
        var vm = new WorkspaceViewModel(); var a = vm.CreateFolder("A"); var b = vm.CreateFolder("B"); var empty = vm.CreateFolder("空文件夹");
        vm.EnterFolder(a); vm.AddSearchFiles([File("same.png")]); vm.EnterFolder(b); vm.AddSearchFiles([File("same.png")]); vm.GoUp();
        var selection = new CollectionSourceSelection(vm.CollectionSnapshot);
        selection.UpdateVisible(vm.VisibleFiles, vm.VisibleFiles.Where(row => row.FolderId != empty));
        Assert.Equal(2, selection.Count); Assert.Single(selection.Bundle.Files);
        selection.BrowseInto(a); Assert.Single(selection.Bundle.Files); Assert.Equal(1, selection.Count);
        selection.Clear(); selection.UpdateVisible(vm.VisibleFiles, vm.VisibleFiles.Where(row => row.FolderId == empty));
        Assert.True(selection.Bundle.HasItems); Assert.Empty(selection.Bundle.Files);
    }
    [Fact]
    public void Source_selection_can_select_and_deselect_an_empty_folder_without_navigating()
    {
        var vm = new WorkspaceViewModel(); var folder = vm.CreateFolder("空文件夹");
        var selection = new CollectionSourceSelection(vm.CollectionSnapshot);
        var row = vm.VisibleFiles.Single();
        selection.UpdateVisible(vm.VisibleFiles, [row]);
        Assert.Equal(Guid.Empty, vm.CurrentFolderId);
        Assert.True(selection.Contains(row)); Assert.Equal(1, selection.Count);
        Assert.Equal(folder, Assert.Single(selection.Bundle.Folders).Id);
        Assert.Empty(selection.Bundle.Files); Assert.True(selection.Bundle.HasItems);
        selection.UpdateVisible(vm.VisibleFiles, []);
        Assert.Equal(0, selection.Count); Assert.False(selection.Bundle.HasItems);
    }
    [Fact]
    public void Source_selection_entering_a_clicked_folder_keeps_other_selections_when_returning()
    {
        var vm = new WorkspaceViewModel(); var folder = vm.CreateFolder("图片");
        vm.AddSearchFiles([File("outside.png")]); vm.EnterFolder(folder); vm.AddSearchFiles([File("inside.png")]); vm.GoUp();
        var selection = new CollectionSourceSelection(vm.CollectionSnapshot);
        selection.UpdateVisible(vm.VisibleFiles, vm.VisibleFiles);
        Assert.Equal(2, selection.Count); Assert.Equal(2, selection.Bundle.Files.Count);
        selection.BrowseInto(folder); vm.EnterFolder(folder);
        Assert.Equal(File("outside.png").FullPath, Assert.Single(selection.Bundle.Files).FullPath);
        selection.UpdateVisible(vm.VisibleFiles, vm.VisibleFiles);
        vm.GoUp();
        Assert.False(selection.Contains(vm.VisibleFiles.Single(row => row.FolderId == folder)));
        Assert.True(selection.Contains(vm.VisibleFiles.Single(row => row.EntryId is not null)));
        Assert.Equal(2, selection.Count); Assert.Equal(2, selection.Bundle.Files.Count);
    }
    [Fact]
    public void Scope_feedback_names_the_mixed_types_and_explains_an_empty_selected_folder()
    {
        var vm = new WorkspaceViewModel(); vm.AddSearchFiles([File("image.png"), File("notes.txt")]); var empty = vm.CreateFolder("空文件夹");
        var conversion = new ConversionViewModel(new Processing(), vm, new(new Interaction()), new());
        Assert.Contains("1 项图片", conversion.FormatFeedback); Assert.Contains("1 项文本文档", conversion.FormatFeedback);
        Assert.False(conversion.CanBuildPlan);
        vm.SetSelection([vm.VisibleFiles.Single(row => row.FolderId == empty)]);
        Assert.Contains("所选文件夹为空", conversion.ScopeSummary); Assert.Contains("所选文件夹为空", vm.SelectionSummary); Assert.False(conversion.CanBuildPlan);
    }
    [Fact]
    public void Invalid_folder_drop_is_rejected_without_changing_the_tree_or_the_previous_undo()
    {
        var vm = new WorkspaceViewModel(); var a = vm.CreateFolder("A"); var child = vm.CreateFolder("child", a); vm.AddSearchFiles([File("one.png")]);
        var before = vm.CollectionSnapshot; var row = vm.VisibleFiles.Single(item => item.FolderId == a);
        Assert.False(vm.CanMoveTo([a], a)); Assert.False(vm.CanMoveTo([a], child));
        Assert.Throws<ArgumentException>(() => vm.MoveItemsTo([row], child));
        Assert.Equal(before.Folders, vm.CollectionSnapshot.Folders); Assert.Equal(before.Entries, vm.CollectionSnapshot.Entries);
        vm.UndoCommand.Execute(null); Assert.Empty(vm.SearchEntries); Assert.Equal(2, vm.CollectionSnapshot.Folders.Count);
    }
    private sealed class PendingReader : IFileInputReader
    {
        private readonly List<TaskCompletionSource<FileImportReport>> pending = [];
        public Task<FileImportReport> ReadAsync(IReadOnlyList<string> paths, CancellationToken token)
        {
            var completion = new TaskCompletionSource<FileImportReport>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(completion); return completion.Task;
        }
        public void Complete(int index, string name) => pending[index].SetResult(new([new(File(name).FullPath, FileImportStatus.Ready, "可用", File(name))]));
    }
    private sealed class Reader : IFileInputReader
    {
        public string? Missing { get; init; }
        public Task<FileImportReport> ReadAsync(IReadOnlyList<string> paths, CancellationToken token) => Task.FromResult(new FileImportReport(paths.Select(path =>
            path == Missing ? new FileImportItem(path, FileImportStatus.Unavailable, "已删除") : new FileImportItem(path, FileImportStatus.Ready, "可用", new(path, false, 10, null))).ToArray()));
    }
    private sealed class Store : IFileCollectionStore
    {
        public bool FailSave { get; set; }
        public bool ReadOnly { get; init; }
        public Task<CollectionLoadResult>? PendingLoad { get; init; }
        public List<FileCollectionSnapshot> Saves { get; } = [];
        public Task<CollectionLoadResult> LoadAsync(CancellationToken token) => PendingLoad ?? Task.FromResult(new CollectionLoadResult(Saves.LastOrDefault(), CanSave: !ReadOnly));
        public Task SaveAsync(FileCollectionSnapshot snapshot, CancellationToken token) { if (FailSave) throw new IOException(); Saves.Add(snapshot); return Task.CompletedTask; }
    }
    private sealed class Processing : IFileProcessingService
    {
        public IReadOnlyList<ProcessingEngineInfo> Engines => [];
        public ConversionRequest? Request { get; private set; }
        public Task<ConversionPlan>? Pending { get; init; }
        public Task<ConversionPlan> CreatePlanAsync(ConversionRequest request, CancellationToken token)
        { Request = request; return Pending ?? Task.FromResult(new ConversionPlan(request, [])); }
        public Task<PreviewResult> PreviewAsync(string path, int edge, CancellationToken token) => throw new NotSupportedException();
        public Task<ConversionReport> ConvertAsync(ConversionPlan plan, IProgress<ProcessingProgress>? progress, CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class Interaction : IFileInteraction
    { public void Open(SearchFile file) { } public void Reveal(SearchFile file) { } public void CopyText(string text) { } }
}
