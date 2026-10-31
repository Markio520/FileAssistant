using FileAssistant.Core.Engines;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Features.Outputs;

public sealed record OutputEntry(int TaskId, string TaskName, ProducedFile Result)
{
    public SearchFile File { get; } = new(Result.FullPath, false, Result.Size, Result.Modified);
    public string Name => File.Name;
    public string FullPath => File.FullPath;
    public string SizeLabel => Workspace.WorkspaceFile.FormatSize(Result.Size);
    public string SourceLabel => string.Join("、", Result.SourcePaths.Select(Path.GetFileName));
    public override string ToString() => $"{Name}，{TaskName}，{FullPath}";
}
