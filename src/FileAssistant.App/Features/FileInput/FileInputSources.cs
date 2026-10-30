using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace FileAssistant.App.Features.FileInput;

/// <summary>Windows input adapters. File validation and metadata belong to IFileInputReader.</summary>
internal static class FileInputSources
{
    public static async Task<IReadOnlyList<string>> PickFilesAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        return (await picker.PickMultipleFilesAsync()).Select(file => file.Path).ToArray();
    }

    public static async Task<IReadOnlyList<string>> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        var folder = await picker.PickSingleFolderAsync();
        return folder is null ? [] : [folder.Path];
    }

    public static bool CanRead(DataPackageView data) =>
        data.Contains(StandardDataFormats.StorageItems) || data.Contains(StandardDataFormats.Text);

    public static async Task<IReadOnlyList<string>> ReadAsync(DataPackageView data)
    {
        if (data.Contains(StandardDataFormats.StorageItems))
            return (await data.GetStorageItemsAsync()).Select(item =>
                string.IsNullOrWhiteSpace(item.Path) ? $"<无本地路径：{item.Name}>" : item.Path).ToArray();
        if (data.Contains(StandardDataFormats.Text))
            return (await data.GetTextAsync()).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return [];
    }
}
