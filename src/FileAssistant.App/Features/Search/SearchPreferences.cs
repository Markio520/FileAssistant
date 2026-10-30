namespace FileAssistant.App.Features.Search;

public sealed record SearchColumnLayout(double Name, double Location, double Size, double Modified);

public sealed record SearchConditions
{
    public string QueryText { get; init; } = string.Empty;
    public int ScopeIndex { get; init; }
    public string Folder { get; init; } = string.Empty;
    public bool IncludeSubfolders { get; init; } = true;
    public bool MatchPath { get; init; }
    public int KindIndex { get; init; }
    public int SizeIndex { get; init; }
    public string CustomMinimum { get; init; } = string.Empty;
    public string CustomMaximum { get; init; } = string.Empty;
    public int SizeUnitIndex { get; init; } = 2;
    public int DateIndex { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateThrough { get; init; }
    public int SortIndex { get; init; }

    public bool IsValid => QueryText is { Length: <= 2048 } && Folder is not null && CustomMinimum is not null && CustomMaximum is not null
        && ScopeIndex is >= 0 and <= 2 && KindIndex is >= 0 and <= 8 && SizeIndex is >= 0 and <= 6
        && SizeUnitIndex is >= 0 and <= 3 && DateIndex is >= 0 and <= 4 && SortIndex is >= 0 and <= 7;
}

public sealed record SavedSearch(string Name, SearchConditions Conditions);

public sealed record SearchPreferences
{
    public int Version { get; init; } = 1;
    public int SortIndex { get; init; }
    public bool ShowDetails { get; init; } = true;
    public SearchColumnLayout? Columns { get; init; }
    public List<SavedSearch> Presets { get; init; } = [];
}
