using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FileAssistant.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Infrastructure.Windows.EverythingRuntime? indexRuntime;
    private Mutex? instanceMutex;
    private bool closing;
    private bool closed;
    public static Features.Workspace.WorkspaceViewModel Workspace { get; private set; } = null!;
    public static Features.Workspace.WorkspaceViewModel CopyInput { get; private set; } = null!;
    public static Features.Search.SearchViewModel Search { get; private set; } = null!;
    public static Core.Engines.IFileProcessingService FileProcessing { get; private set; } = null!;
    public static Features.Convert.ConversionViewModel Conversion { get; private set; } = null!;
    public static Features.Outputs.OutputsViewModel Outputs { get; private set; } = null!;
    public static Features.Tasks.ProcessingJobsViewModel Jobs { get; private set; } = null!;
    public static Features.Settings.EnginesViewModel EngineSettings { get; private set; } = null!;
    /// <summary>
    /// The main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>
    /// The UI thread dispatcher. Use <c>App.DispatcherQueue</c> to marshal calls
    /// to the UI thread. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>
    /// The native window handle (HWND). Use for file pickers,
    /// <c>DataTransferManager</c>, and any WinRT interop that requires
    /// <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        var arguments = Environment.GetCommandLineArgs();
        if (arguments.Length == 3 && arguments[1] == Infrastructure.Windows.EverythingDistribution.SetupArgument)
            Environment.Exit(Infrastructure.Windows.EverythingServiceSetup.Install(arguments[2]));
        InitializeComponent();
        UnhandledException += (_, error) =>
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileAssistant", "Logs");
            try { Directory.CreateDirectory(directory); File.AppendAllText(Path.Combine(directory, "app-errors.log"), $"{DateTimeOffset.Now:O}\n{error.Exception}\n"); }
            catch (Exception) { }
        };
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var instance = Infrastructure.Windows.EverythingDistribution.UserInstance;
        instanceMutex = new Mutex(false, @"Local\" + instance + ".App");
        bool ownsInstance;
        try { ownsInstance = instanceMutex.WaitOne(0); }
        catch (AbandonedMutexException) { ownsInstance = true; }
        if (!ownsInstance)
        {
            foreach (var running in System.Diagnostics.Process.GetProcessesByName("FileAssistant.App"))
            {
                using (running)
                    if (running.Id != Environment.ProcessId && running.MainWindowHandle != 0)
                    {
                        ShowWindow(running.MainWindowHandle, 9);
                        SetForegroundWindow(running.MainWindowHandle);
                        break;
                    }
            }
            Exit();
            return;
        }
        var inputReader = new Infrastructure.FileSystem.FileInputReader();
        Workspace = new(inputReader);
        CopyInput = new(inputReader, isStaging: false);
        FileProcessing = new Infrastructure.Engines.LocalFileProcessingService(Infrastructure.Engines.EnginePaths.Installed);
        EngineSettings = new(FileProcessing, new Infrastructure.Engines.EnginePackageInstaller(new HttpClient { Timeout = TimeSpan.FromMinutes(10) },
            Infrastructure.Engines.EnginePaths.ExtensionsDirectory), Infrastructure.Engines.EnginePackageCatalog.Ffmpeg, Infrastructure.Engines.EnginePackageCatalog.LibreOffice);
        Jobs = new();
        Outputs = new(new Infrastructure.Windows.WindowsFileInteraction());
        Conversion = new(FileProcessing, new(inputReader, Features.Workspace.WorkspaceInputPurpose.Conversion), Outputs, Jobs);
        EngineSettings.AvailabilityChanged += (_, _) => Conversion.RefreshCapabilities();
        indexRuntime = new(Infrastructure.Windows.EverythingDistribution.BundledExecutable,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileAssistant", "Search", "Everything-1.4"), instance);
        var searchService = new Infrastructure.FileSystem.FileSearchService(
            new Infrastructure.Windows.EverythingSearchClient(instance), new Infrastructure.FileSystem.DirectorySearchIndex(), indexRuntime);
        Search = new(searchService, () => Workspace.SearchEntries, Workspace.ImportFilesAsync, new Infrastructure.Windows.WindowsFileInteraction(),
            new Infrastructure.Persistence.JsonSettingsStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileAssistant", "search-settings.json")));
        await Search.LoadPreferencesAsync();
        Window = new MainWindow();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Window.Activate();
        Window.AppWindow.Closing += async (_, e) =>
        {
            if (closed) return;
            e.Cancel = true;
            if (closing) return;
            closing = true;
            Search.Deactivate();
            Workspace.CancelLoadingCommand.Execute(null);
            CopyInput.CancelLoadingCommand.Execute(null);
            Conversion.Input.CancelLoadingCommand.Execute(null);
            Conversion.CancelCommand.Execute(null);
            EngineSettings.CancelCommand.Execute(null);
            Window.AppWindow.Hide();
            try
            {
                if (Conversion.RunCommand.ExecutionTask is { } processing) await processing;
                if (EngineSettings.DownloadVideoCommand.ExecutionTask is { } download) await download;
                await Search.FlushPreferencesAsync(); await indexRuntime.DisposeAsync();
            }
            finally { closed = true; instanceMutex.ReleaseMutex(); instanceMutex.Dispose(); Window.Close(); }
        };
        _ = WarmIndexAsync();
    }

    private async Task WarmIndexAsync()
    {
        try { await indexRuntime!.EnsureReadyAsync(null, CancellationToken.None); }
        catch (Exception) { /* The search view presents the retained failure and an explicit retry. */ }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
}
