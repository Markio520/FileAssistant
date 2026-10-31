using FileAssistant.App.Controls;
using FileAssistant.App.Features.FileInput;

namespace FileAssistant.App.Features.Workspace;

public static class WorkspaceSelectionOptions
{
    public static IReadOnlyList<SelectionOption> Types => FileTypeFilterOptions.All;
    public static IReadOnlyList<SelectionOption> Conflicts { get; } =
    [
        new(0, "自动编号，保留两个文件"), new(1, "跳过同名文件"), new(2, "覆盖前逐项确认")
    ];
}
