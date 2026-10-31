using CommunityToolkit.Mvvm.ComponentModel;

namespace FileAssistant.App.Features.FileInput;

public partial class FileInputColumnWidths : ObservableObject
{
    private bool resized;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MinimumTableWidth))] public partial double Name { get; set; } = 300;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MinimumTableWidth))] public partial double Type { get; set; } = 100;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MinimumTableWidth))] public partial double Size { get; set; } = 80;
    // Cell spacing, insets and vertical scrollbar.
    public double MinimumTableWidth => Name + Type + Size + 76;

    public void FitViewport(double width)
    {
        if (!resized && double.IsFinite(width)) Name = Math.Max(180, width - Type - Size - 76);
    }

    public void Resize(string column, double delta)
    {
        if (!double.IsFinite(delta)) return;
        resized = true;
        switch (column)
        {
            case "Name": Name = Math.Clamp(Name + delta, 180, 4096); break;
            case "Type": Type = Math.Clamp(Type + delta, 64, 4096); break;
            case "Size": Size = Math.Clamp(Size + delta, 64, 4096); break;
        }
    }
}
