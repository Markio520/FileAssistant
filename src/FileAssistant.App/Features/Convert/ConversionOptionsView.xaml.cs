using FileAssistant.App.Features.FileInput;
using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileAssistant.App.Features.Convert;

public sealed partial class ConversionOptionsView : UserControl
{
    private readonly DispatcherQueueTimer previewTimer;
    private ConversionViewModel? observedModel;
    private bool previewActive;
    private bool previewPending;
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel), typeof(ConversionViewModel),
        typeof(ConversionOptionsView), new PropertyMetadata(null, (owner, _) =>
        {
            var view = (ConversionOptionsView)owner;
            view.Bindings.Update();
            view.AttachPreviewModel();
        }));
    public ConversionViewModel ViewModel { get => (ConversionViewModel)GetValue(ViewModelProperty); set => SetValue(ViewModelProperty, value); }
    public ConversionOptionsView()
    {
        InitializeComponent();
        previewTimer = DispatcherQueue.CreateTimer();
        previewTimer.Interval = TimeSpan.FromMilliseconds(400);
        previewTimer.IsRepeating = false;
        previewTimer.Tick += PreviewTimer_Tick;
        Loaded += (_, _) => AttachPreviewModel();
        Unloaded += (_, _) => { previewTimer.Stop(); DetachPreviewModel(); };
    }

    public void SetPreviewActive(bool active)
    {
        previewActive = active;
        if (active && ViewModel?.Plan is null) RequestPreview();
        else if (!active) previewTimer.Stop();
    }

    private void AttachPreviewModel()
    {
        if (!IsLoaded || ReferenceEquals(observedModel, ViewModel)) return;
        DetachPreviewModel();
        observedModel = ViewModel;
        if (observedModel is null) return;
        observedModel.PlanInvalidated += PlanInvalidated;
        observedModel.PropertyChanged += PreviewModelChanged;
        if (observedModel.Plan is null) RequestPreview();
    }
    private void DetachPreviewModel()
    {
        if (observedModel is null) return;
        observedModel.PlanInvalidated -= PlanInvalidated;
        observedModel.PropertyChanged -= PreviewModelChanged;
        observedModel = null;
    }
    private void PlanInvalidated(object? sender, EventArgs e) => RequestPreview();
    private void PreviewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ConversionViewModel.IsBusy) || ViewModel.IsBusy || !previewPending) return;
        if (ViewModel.Plan is null) RequestPreview();
        else { previewPending = false; previewTimer.Stop(); }
    }
    private void RequestPreview()
    {
        previewPending = true;
        if (!previewActive || !IsLoaded) return;
        previewTimer.Stop();
        previewTimer.Start();
    }
    private async void PreviewTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (!previewActive || !IsLoaded || ViewModel is null || !previewPending) return;
        if (ViewModel.IsBusy || ViewModel.BuildPlanCommand.IsRunning) return;
        previewPending = false;
        if (ViewModel.CanBuildPlan) await ViewModel.BuildPlanCommand.ExecuteAsync(null);
    }
    private async void PickOutput_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var paths = await FileInputSources.PickFolderAsync();
            if (paths.Count > 0) ViewModel.OutputDirectory = paths[0];
        }
        catch (Exception) { ViewModel.Feedback = "无法打开目录选择器，也可以直接填写输出文件夹。"; }
    }
}
