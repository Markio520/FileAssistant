using System.Text.Json;
using FileAssistant.App.Features.Search;
using FileAssistant.Core.Abstractions;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Tests;

public sealed class SearchPreferencesTests
{
    [Fact]
    public async Task Preferences_restore_layout_and_named_conditions_without_restoring_file_selection()
    {
        var store = new MemorySettings();
        var first = Create(store);
        first.ColumnWidths.Resize("Name", 80); first.ColumnWidths.Resize("Location", 40);
        first.SortIndex = 7; first.ShowDetails = false;
        first.ScopeIndex = 1; first.Folder = @"D:\素材"; first.QueryText = "产品 ext:png";
        first.KindIndex = (int)SearchKind.Images; first.MatchPath = true; first.IncludeSubfolders = false;
        first.SizeIndex = 6; first.CustomMinimum = "1.5"; first.CustomMaximum = "20";
        first.DateIndex = 4; first.CustomDateThrough = new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.FromHours(8));
        first.PresetName = "本月产品图";
        await first.SavePresetCommand.ExecuteAsync(null);

        var service = new SearchExperienceTests.RecordingSearch();
        var restored = Create(store, service);
        await restored.LoadPreferencesAsync();
        Assert.Equal(7, restored.SortIndex);
        Assert.False(restored.ShowDetails);
        Assert.Equal(340, restored.ColumnWidths.Name);
        Assert.Equal(300, restored.ColumnWidths.Location);
        restored.ColumnWidths.FitViewport(1400);
        Assert.Equal(300, restored.ColumnWidths.Location);
        Assert.Empty(restored.QueryText);
        Assert.Empty(restored.Results);
        Assert.Equal(0, restored.SelectionCount);
        Assert.Equal("本月产品图", restored.PresetOptions[1].Label);
        restored.Activate();
        restored.PresetIndex = 0;
        await SearchExperienceTests.Settled(restored);
        Assert.Equal(2, service.Calls); // One initial query and one complete preset, no intermediate conditions.
        Assert.Equal("产品 ext:png", service.Last!.Text);
        Assert.Equal(SearchScope.Folder, service.Last.Scope);
        Assert.Equal(@"D:\素材", service.Last.Folder);
        Assert.False(service.Last.IncludeSubfolders);
        Assert.True(service.Last.MatchPath);
        Assert.Equal(1572864, service.Last.MinimumSize);
        Assert.Equal(20971520, service.Last.MaximumSize);
        Assert.Equal(new DateTime(2026, 10, 31), service.Last.ModifiedBefore!.Value.LocalDateTime);
        Assert.Equal(0, restored.PresetIndex);
        await restored.FlushPreferencesAsync();
    }

    [Fact]
    public async Task Rapid_layout_changes_are_coalesced_and_shutdown_flushes_the_latest_values()
    {
        var store = new MemorySettings();
        var vm = Create(store);
        for (var i = 0; i < 100; i++) vm.ColumnWidths.Resize("Name", 1);
        vm.SortIndex = 4; vm.SortIndex = 5; vm.ShowDetails = false;
        Assert.Equal(0, store.Writes);
        await vm.FlushPreferencesAsync();
        Assert.Equal(1, store.Writes);
        var saved = JsonSerializer.Deserialize<SearchPreferences>(store.Document!)!;
        Assert.Equal(360, saved.Columns!.Name);
        Assert.Equal(5, saved.SortIndex);
        Assert.False(saved.ShowDetails);
        await Task.Delay(450);
        Assert.Equal(1, store.Writes); // Cancelled delayed writes must not overwrite the flushed document.
    }

    [Fact]
    public async Task Layout_changes_are_saved_after_the_debounce_without_needing_shutdown()
    {
        var store = new MemorySettings();
        var vm = Create(store);
        vm.ColumnWidths.Resize("Size", 16); vm.SortIndex = 6;
        Assert.Equal(0, store.Writes);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while (store.Writes == 0) await Task.Delay(20, timeout.Token);
        Assert.Equal(6, JsonSerializer.Deserialize<SearchPreferences>(store.Document!)!.SortIndex);
        Assert.Equal(1, store.Writes);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"Version\":99}")]
    [InlineData("{\"SortIndex\":99}")]
    [InlineData("{\"Presets\":[null]}")]
    [InlineData("{\"Presets\":[{\"Name\":\"bad\",\"Conditions\":{\"KindIndex\":99}}]}")]
    public async Task Damaged_or_unknown_settings_show_feedback_without_blocking_search(string document)
    {
        var store = new MemorySettings { Document = document };
        var vm = Create(store);
        await vm.LoadPreferencesAsync();
        Assert.True(vm.HasPreferencesError);
        Assert.Equal(0, vm.SortIndex);
        Assert.Single(vm.PresetOptions);
        vm.Activate();
        Assert.Single(vm.Results);
        Assert.Equal(document, store.Document); // Loading itself never overwrites damaged data.
    }

    [Fact]
    public async Task Save_failures_keep_named_conditions_in_memory_and_retry_persists_them()
    {
        var store = new MemorySettings { FailWrites = true };
        var vm = Create(store);
        vm.PresetName = "图片"; vm.KindIndex = (int)SearchKind.Images;
        await vm.SavePresetCommand.ExecuteAsync(null);
        Assert.True(vm.HasPreferencesError);
        Assert.Equal(2, vm.PresetOptions.Count);
        Assert.Contains("本次运行", vm.PresetMessage);
        store.FailWrites = false;
        await vm.SavePreferencesCommand.ExecuteAsync(null);
        Assert.False(vm.HasPreferencesError);
        Assert.Equal("图片", Assert.Single(JsonSerializer.Deserialize<SearchPreferences>(store.Document!)!.Presets).Name);
    }

    [Fact]
    public async Task Saving_rejects_duplicates_and_invalid_conditions_and_removing_keeps_current_query()
    {
        var store = new MemorySettings();
        var vm = Create(store);
        vm.PresetName = "条件"; vm.QueryText = "size:bad";
        await vm.SavePresetCommand.ExecuteAsync(null);
        Assert.Single(vm.PresetOptions);
        Assert.Equal(0, store.Writes);
        vm.QueryText = "产品";
        await vm.SavePresetCommand.ExecuteAsync(null);
        await vm.SavePresetCommand.ExecuteAsync(null);
        Assert.Contains("同名", vm.PresetMessage);
        Assert.Equal(2, vm.PresetOptions.Count);
        await vm.RemovePresetCommand.ExecuteAsync(null);
        Assert.Equal("产品", vm.QueryText);
        Assert.Single(vm.PresetOptions);
        Assert.Empty(JsonSerializer.Deserialize<SearchPreferences>(store.Document!)!.Presets);
    }

    [Fact]
    public async Task Restored_column_widths_are_clamped_to_usable_bounds()
    {
        var store = new MemorySettings { Document = JsonSerializer.Serialize(new SearchPreferences { Columns = new(-1, 99999, 0, 12) }) };
        var vm = Create(store);
        await vm.LoadPreferencesAsync();
        Assert.False(vm.HasPreferencesError);
        Assert.Equal(120, vm.ColumnWidths.Name);
        Assert.Equal(4096, vm.ColumnWidths.Location);
        Assert.Equal(64, vm.ColumnWidths.Size);
        Assert.Equal(112, vm.ColumnWidths.Modified);
        Assert.Equal(0, store.Writes);
    }

    private static SearchViewModel Create(MemorySettings store, SearchExperienceTests.RecordingSearch? service = null)
        => new(service ?? new(), () => [], TestImports.None, settings: store);

    private sealed class MemorySettings : ISettingsStore
    {
        public string? Document;
        public int Writes;
        public bool FailWrites;
        public Task<string?> ReadAsync() => Task.FromResult(Document);
        public Task WriteAsync(string contents)
        {
            if (FailWrites) throw new IOException("test failure");
            Document = contents; Writes++;
            return Task.CompletedTask;
        }
    }
}
