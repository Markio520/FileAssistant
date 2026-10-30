using FileAssistant.App.Controls;

namespace FileAssistant.App.Features.Workspace;

public static class WorkspaceSelectionOptions
{
    public static IReadOnlyList<SelectionOption> Types { get; } =
    [
        new(0, "所有类型"), new(1, "图片"), new(2, "文档"), new(3, "压缩包"), new(4, "视频")
    ];
    public static IReadOnlyList<SelectionOption> Conflicts { get; } =
    [
        new(0, "自动编号，保留两个文件"), new(1, "跳过同名文件"), new(2, "覆盖前逐项确认")
    ];
}
