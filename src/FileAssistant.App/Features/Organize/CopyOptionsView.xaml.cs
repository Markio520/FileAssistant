using System.ComponentModel;
using FileAssistant.App.Features.FileInput;
using FileAssistant.Core.Features.Organize;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileAssistant.App.Features.Organize;

public sealed partial class CopyOptionsView : UserControl
{
    public CopyViewModel ViewModel => App.Copy;
    private readonly DispatcherQueueTimer timer;
    private bool active, pending;
    public CopyOptionsView()
    {
        InitializeComponent();
        timer = DispatcherQueue.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(400); timer.IsRepeating = false;
        timer.Tick += Preview_Tick;
        Loaded += (_, _) => { ViewModel.PlanInvalidated += Invalidated; ViewModel.PropertyChanged += Changed; Request(); _ = ViewModel.RefreshRecoveriesCommand.ExecuteAsync(null); };
        Unloaded += (_, _) => { timer.Stop(); ViewModel.PlanInvalidated -= Invalidated; ViewModel.PropertyChanged -= Changed; };
    }
    public void SetActive(bool value) { active = value; if (value && ViewModel.Plan is null) Request(); else if (!value) timer.Stop(); }
    private void Invalidated(object? sender, EventArgs e) => Request();
    private void Changed(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(CopyViewModel.IsBusy) && !ViewModel.IsBusy && pending) Request(); }
    private void Request() { pending = true; if (active && IsLoaded) { timer.Stop(); timer.Start(); } }
    private async void Preview_Tick(DispatcherQueueTimer sender, object e)
    {
        if (!active || !IsLoaded || !pending || ViewModel.IsBusy || ViewModel.BuildPlanCommand.IsRunning) return;
        pending = false;
        if (ViewModel.Plan is not null) return;
        if (ViewModel.CanBuild) await ViewModel.BuildPlanCommand.ExecuteAsync(null);
    }
    private async void PickDestination_Click(object sender, RoutedEventArgs e)
    {
        try { var paths = await FileInputSources.PickFolderAsync(); if (paths.Count > 0) ViewModel.Destination = paths[0]; }
        catch (Exception error) { ViewModel.Feedback = "无法选择目录，也可直接填写：" + error.Message; }
    }
    private async void Decide_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPlanItem is { } item && Enum.TryParse<CopyDecision>((string)((Button)sender).Tag, out var decision))
            await ViewModel.DecideAsync(item.Id, decision);
    }
    private async void Restore_Click(object sender, RoutedEventArgs e)
    { if (RecoveryList.SelectedItem is CopyRecovery record) await ViewModel.RecoverAsync(record.Id, false); else ViewModel.Feedback = "请先选择一条恢复记录。"; }
    private async void Discard_Click(object sender, RoutedEventArgs e)
    {
        if (RecoveryList.SelectedItem is not CopyRecovery record) { ViewModel.Feedback = "请先选择一条恢复记录。"; return; }
        var dialog = new ContentDialog { XamlRoot = XamlRoot, RequestedTheme = ActualTheme, Title = "删除这份覆盖备份？",
            Content = "删除后无法通过此记录恢复旧目标，当前目标文件保留。\n" + record.Backup, PrimaryButtonText = "删除备份", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) await ViewModel.RecoverAsync(record.Id, true);
    }
}
