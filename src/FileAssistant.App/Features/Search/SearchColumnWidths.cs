using CommunityToolkit.Mvvm.ComponentModel;

namespace FileAssistant.App.Features.Search;

public partial class SearchColumnWidths : ObservableObject
{
    private bool resized;
    public bool HasUserWidths => resized;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MinimumTableWidth))] public partial double Name { get; set; } = 260;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MinimumTableWidth))] public partial double Location { get; set; } = 260;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MinimumTableWidth))] public partial double Size { get; set; } = 80;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MinimumTableWidth))] public partial double Modified { get; set; } = 132;
    // Cell spacing, left/right insets, and room for the vertical scrollbar.
    public double MinimumTableWidth => Name + Location + Size + Modified + 76;

    public void FitViewport(double width)
    {
        if (!resized && double.IsFinite(width)) Location = Math.Max(160, width - Name - Size - Modified - 76);
    }

    public void Resize(string column, double delta)
    {
        if (!double.IsFinite(delta)) return;
        resized = true;
        switch (column)
        {
            case "Name": Name = Math.Clamp(Name + delta, 120, 4096); break;
            case "Location": Location = Math.Clamp(Location + delta, 160, 4096); break;
            case "Size": Size = Math.Clamp(Size + delta, 64, 4096); break;
            case "Modified": Modified = Math.Clamp(Modified + delta, 112, 4096); break;
        }
    }

    public SearchColumnLayout? Capture() => resized ? new(Name, Location, Size, Modified) : null;

    public void Restore(SearchColumnLayout layout)
    {
        resized = true;
        Name = double.IsFinite(layout.Name) ? Math.Clamp(layout.Name, 120, 4096) : 260;
        Location = double.IsFinite(layout.Location) ? Math.Clamp(layout.Location, 160, 4096) : 260;
        Size = double.IsFinite(layout.Size) ? Math.Clamp(layout.Size, 64, 4096) : 80;
        Modified = double.IsFinite(layout.Modified) ? Math.Clamp(layout.Modified, 112, 4096) : 132;
    }
}
