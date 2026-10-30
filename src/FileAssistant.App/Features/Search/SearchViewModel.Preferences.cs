using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileAssistant.App.Controls;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Features.Search;

public partial class SearchViewModel
{
    private readonly List<SavedSearch> presets = [];
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private CancellationTokenSource? saveCancellation;
    private Task pendingSave = Task.CompletedTask;
    private int preferencesVersion;
    private bool loadingPreferences;
    private bool preferencesDirty;
    public ObservableCollection<SelectionOption> PresetOptions { get; } = [new(-1, "选择常用条件")];
    [ObservableProperty] public partial bool ShowDetails { get; set; } = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanRemovePreset))] public partial int PresetIndex { get; set; } = -1;
    [ObservableProperty] public partial string PresetName { get; set; } = string.Empty;
    [ObservableProperty] public partial string PresetMessage { get; set; } = string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasPreferencesError))] public partial string PreferencesError { get; set; } = string.Empty;
    public bool HasPreferencesError => PreferencesError.Length > 0;
    public bool CanRemovePreset => PresetIndex >= 0 && PresetIndex < presets.Count;

    partial void OnShowDetailsChanged(bool value) => QueuePreferencesSave();
    partial void OnPresetIndexChanged(int value)
    {
        if (updatingConditions || value < 0 || value >= presets.Count) return;
        var preset = presets[value];
        ApplyConditions(preset.Conditions);
        PresetName = preset.Name;
        PresetMessage = $"已套用“{preset.Name}”。";
    }

    private SearchConditions CaptureConditions() => new()
    {
        QueryText = QueryText, ScopeIndex = ScopeIndex, Folder = Folder, IncludeSubfolders = IncludeSubfolders,
        MatchPath = MatchPath, KindIndex = KindIndex, SizeIndex = SizeIndex, CustomMinimum = CustomMinimum,
        CustomMaximum = CustomMaximum, SizeUnitIndex = SizeUnitIndex, DateIndex = DateIndex,
        DateFrom = CustomDateFrom is { } from ? DateOnly.FromDateTime(from.Date) : null,
        DateThrough = CustomDateThrough is { } through ? DateOnly.FromDateTime(through.Date) : null, SortIndex = SortIndex
    };

    private void ApplyConditions(SearchConditions conditions)
    {
        updatingConditions = true;
        QueryText = conditions.QueryText; ScopeIndex = conditions.ScopeIndex; Folder = conditions.Folder;
        IncludeSubfolders = conditions.IncludeSubfolders; MatchPath = conditions.MatchPath; KindIndex = conditions.KindIndex;
        SizeIndex = conditions.SizeIndex; CustomMinimum = conditions.CustomMinimum; CustomMaximum = conditions.CustomMaximum;
        SizeUnitIndex = conditions.SizeUnitIndex; DateIndex = conditions.DateIndex;
        CustomDateFrom = conditions.DateFrom is { } from ? LocalMidnight(from.ToDateTime(TimeOnly.MinValue)) : null;
        CustomDateThrough = conditions.DateThrough is { } through ? LocalMidnight(through.ToDateTime(TimeOnly.MinValue)) : null;
        SortIndex = conditions.SortIndex;
        updatingConditions = false;
        OnPropertyChanged(nameof(FilterError)); OnPropertyChanged(nameof(HasFilterError));
        NotifyResultActions();
        if (active && !composing) _ = RunAsync(false, true);
    }

    [RelayCommand] private async Task SavePresetAsync()
    {
        var name = PresetName.Trim();
        if (name.Length is 0 or > 60) { PresetMessage = "请输入 1–60 个字符的条件名称。"; return; }
        if (presets.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
        { PresetMessage = "已有同名条件，请换一个名称，或先移除原条件。"; return; }
        if (!HasSearchSource) { PresetMessage = "请先填写搜索文件夹路径。"; return; }
        if (Request() is not { } request) { PresetMessage = FilterError; return; }
        try { _ = SearchRules.Compile(request); }
        catch (SearchException e) { PresetMessage = e.Message; return; }
        presets.Add(new(name, CaptureConditions()));
        RebuildPresetOptions();
        updatingConditions = true; PresetIndex = presets.Count - 1; updatingConditions = false;
        preferencesDirty = true;
        await SavePreferencesAsync();
        PresetMessage = HasPreferencesError ? "已保留在本次运行中，请重试保存。" : $"已保存“{name}”。";
    }

    [RelayCommand] private async Task RemovePresetAsync()
    {
        if (!CanRemovePreset) return;
        var name = presets[PresetIndex].Name;
        presets.RemoveAt(PresetIndex);
        RebuildPresetOptions();
        preferencesDirty = true;
        await SavePreferencesAsync();
        PresetMessage = HasPreferencesError ? "已从本次列表移除，请重试保存。" : $"已移除“{name}”，当前搜索不变。";
    }

    private void RebuildPresetOptions()
    {
        PresetIndex = -1;
        PresetOptions.Clear();
        PresetOptions.Add(new(-1, "选择常用条件"));
        for (var index = 0; index < presets.Count; index++) PresetOptions.Add(new(index, presets[index].Name));
        OnPropertyChanged(nameof(CanRemovePreset));
    }

    public async Task LoadPreferencesAsync()
    {
        if (settings is null) return;
        loadingPreferences = true;
        try
        {
            var json = await settings.ReadAsync();
            if (json is null) return;
            var preferences = JsonSerializer.Deserialize<SearchPreferences>(json);
            if (preferences is null || preferences.Version != 1 || preferences.SortIndex is < 0 or > 7
                || preferences.Presets is null || preferences.Presets.Any(p => p is null || string.IsNullOrWhiteSpace(p.Name)
                    || p.Name.Length > 60 || p.Conditions is not { IsValid: true })) throw new JsonException();
            SortIndex = preferences.SortIndex;
            ShowDetails = preferences.ShowDetails;
            if (preferences.Columns is { } columns) ColumnWidths.Restore(columns);
            presets.Clear(); presets.AddRange(preferences.Presets);
            RebuildPresetOptions();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        { PreferencesError = "无法读取已保存的搜索设置，本次使用默认设置。修改后可重新保存。"; }
        finally { loadingPreferences = false; }
    }

    private void QueuePreferencesSave()
    {
        if (settings is null || loadingPreferences) return;
        preferencesDirty = true;
        StartPreferencesSave(debounce: true);
    }

    [RelayCommand] private Task SavePreferencesAsync()
    {
        preferencesDirty = true;
        return StartPreferencesSave(debounce: false);
    }

    public Task FlushPreferencesAsync() => preferencesDirty ? StartPreferencesSave(debounce: false) : pendingSave;

    private Task StartPreferencesSave(bool debounce)
    {
        if (settings is null) return Task.CompletedTask;
        saveCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        saveCancellation = cancellation;
        var version = ++preferencesVersion;
        return pendingSave = WritePreferencesAsync(debounce, cancellation, version);
    }

    private async Task WritePreferencesAsync(bool debounce, CancellationTokenSource cancellation, int version)
    {
        using (cancellation)
        {
            try
            {
                if (debounce) await Task.Delay(400, cancellation.Token);
                await saveGate.WaitAsync(cancellation.Token);
                try
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    var document = new SearchPreferences { SortIndex = SortIndex, ShowDetails = ShowDetails,
                        Columns = ColumnWidths.Capture(), Presets = [.. presets] };
                    await settings!.WriteAsync(JsonSerializer.Serialize(document));
                    if (version == preferencesVersion) { preferencesDirty = false; PreferencesError = string.Empty; }
                }
                finally { saveGate.Release(); }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
            { if (version == preferencesVersion) PreferencesError = "无法保存搜索设置；本次操作仍可继续，请重试保存。"; }
            finally { if (version == preferencesVersion) saveCancellation = null; }
        }
    }
}
