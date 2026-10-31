using FileAssistant.App.Features.Workspace;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Tests;

public sealed class WorkspaceFilterTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void All_type_choices_filter_staging_and_independent_inputs(bool isStaging)
    {
        SearchFile[] files =
        [
            new(@"D:\image.JPG", false, 10, null), new(@"D:\document.PDF", false, 10, null),
            new(@"D:\archive.7z", false, 10, null), new(@"D:\video.mp4", false, 10, null),
            new(@"D:\audio.FLAC", false, 10, null), new(@"D:\tool.EXE", false, 10, null),
            new(@"D:\unknown.bin", false, 10, null), new(@"D:\folder.jpg", true, null, null)
        ];
        var expected = new Dictionary<SearchKind, string[]>
        {
            [SearchKind.All] = files.Select(file => file.Name).ToArray(),
            [SearchKind.Audio] = ["audio.FLAC"], [SearchKind.Archives] = ["archive.7z"],
            [SearchKind.Documents] = ["document.PDF"], [SearchKind.Executables] = ["tool.EXE"],
            [SearchKind.Folders] = ["folder.jpg"], [SearchKind.Images] = ["image.JPG"],
            [SearchKind.Video] = ["video.mp4"],
            [SearchKind.Files] = files.Where(file => !file.IsFolder).Select(file => file.Name).ToArray()
        };
        var viewModel = new WorkspaceViewModel(isStaging: isStaging);
        viewModel.AddSearchFiles(files);

        Assert.Equal(expected.Count, WorkspaceSelectionOptions.Types.Count);
        foreach (var option in WorkspaceSelectionOptions.Types)
        {
            viewModel.FilterIndex = option.Value;
            Assert.Equal(expected[(SearchKind)option.Value], viewModel.VisibleFiles.Select(file => file.Name));
            Assert.Equal(files.Length, viewModel.SearchEntries.Count);
        }

        viewModel.SearchText = "audio";
        viewModel.FilterIndex = (int)SearchKind.Audio;
        Assert.Equal("audio.FLAC", Assert.Single(viewModel.VisibleFiles).Name);
        viewModel.FilterIndex = (int)SearchKind.Folders;
        Assert.Empty(viewModel.VisibleFiles);
        viewModel.SearchText = string.Empty;
        Assert.True(Assert.Single(viewModel.VisibleFiles).Source!.IsFolder);
    }
}
