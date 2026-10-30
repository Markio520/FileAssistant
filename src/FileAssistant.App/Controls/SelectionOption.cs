namespace FileAssistant.App.Controls;

/// <summary>A stable value and its presentation, independent of menu order.</summary>
public sealed record SelectionOption(int Value, string Text, string? ButtonText = null)
{
    public string Label => ButtonText ?? Text;
}
