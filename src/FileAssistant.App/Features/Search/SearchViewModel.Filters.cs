using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Features.Search;

public partial class SearchViewModel
{
    private bool updatingConditions;
    [ObservableProperty] public partial string CustomMinimum { get; set; } = string.Empty;
    [ObservableProperty] public partial string CustomMaximum { get; set; } = string.Empty;
    [ObservableProperty] public partial int SizeUnitIndex { get; set; } = 2;
    [ObservableProperty] public partial DateTimeOffset? CustomDateFrom { get; set; }
    [ObservableProperty] public partial DateTimeOffset? CustomDateThrough { get; set; }
    public bool IsCustomSize => SizeIndex == 6;
    public bool IsCustomDate => DateIndex == 4;
    public string FilterError { get { TryGetCustomRanges(out _, out _, out _, out _, out var error); return error; } }
    public bool HasFilterError => FilterError.Length > 0;
    public string NameSortGlyph => SortGlyph(SearchSort.Name);
    public string PathSortGlyph => SortGlyph(SearchSort.Path);
    public string SizeSortGlyph => SortGlyph(SearchSort.Size);
    public string ModifiedSortGlyph => SortGlyph(SearchSort.Modified);
    public string NameSortDescription => SortDescription(SearchSort.Name, "名称");
    public string PathSortDescription => SortDescription(SearchSort.Path, "所在位置");
    public string SizeSortDescription => SortDescription(SearchSort.Size, "大小");
    public string ModifiedSortDescription => SortDescription(SearchSort.Modified, "修改时间");

    partial void OnCustomMinimumChanged(string value) => Schedule();
    partial void OnCustomMaximumChanged(string value) => Schedule();
    partial void OnSizeUnitIndexChanged(int value) => Schedule();
    partial void OnCustomDateFromChanged(DateTimeOffset? value) => Schedule();
    partial void OnCustomDateThroughChanged(DateTimeOffset? value) => Schedule();

    public void SortBy(SearchSort column)
        => SortIndex = SortIndex / 2 == (int)column ? SortIndex ^ 1 : (int)column * 2;

    private string SortGlyph(SearchSort column) => SortIndex / 2 != (int)column ? string.Empty : SortIndex % 2 == 0 ? "\uE70E" : "\uE70D";
    private string SortDescription(SearchSort column, string label) => SortIndex / 2 != (int)column
        ? $"按{label}升序排序" : $"{label}，当前{(SortIndex % 2 == 0 ? "升序，点击改为降序" : "降序，点击改为升序")}";

    private bool TryGetCustomRanges(out long? minimum, out long? maximum, out DateTimeOffset? from,
        out DateTimeOffset? before, out string error)
    {
        minimum = maximum = null; from = before = null; error = string.Empty;
        if (IsCustomSize)
        {
            if (!TrySize(CustomMinimum, true, out minimum) || !TrySize(CustomMaximum, false, out maximum))
                error = "大小请输入非负数字，最多支持 9,223,372,036,854,775,807 字节。";
            else if (minimum is null && maximum is null) error = "填写最小或最大大小；留空的一端表示不限。";
            else if (minimum > maximum) error = "最小大小不能大于最大大小，请调整区间。";
        }
        if (error.Length == 0 && IsCustomDate)
        {
            if (CustomDateFrom is null && CustomDateThrough is null) error = "选择开始或结束日期；结束日期包含当天。";
            else if (CustomDateFrom?.Date > CustomDateThrough?.Date) error = "开始日期不能晚于结束日期，请调整区间。";
            else if (CustomDateThrough?.Date == DateTime.MaxValue.Date) error = "结束日期需早于 9999 年 12 月 31 日。";
            else
            {
                if (CustomDateFrom is { } start) from = LocalMidnight(start.Date);
                if (CustomDateThrough is { } end) before = LocalMidnight(end.Date.AddDays(1));
            }
        }
        return error.Length == 0;
    }

    private bool TrySize(string text, bool lowerBound, out long? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        var factor = SizeUnitIndex switch { 0 => 1L, 1 => 1024L, 2 => 1048576L, 3 => 1073741824L, _ => 0L };
        if (factor == 0 || !decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out var value)
            || value < 0 || value > (decimal)long.MaxValue / factor) return false;
        var bytes = value * factor;
        result = (long)(lowerBound ? decimal.Ceiling(bytes) : decimal.Floor(bytes));
        return true;
    }

    private static DateTimeOffset LocalMidnight(DateTime day) => new(DateTime.SpecifyKind(day, DateTimeKind.Unspecified));
}
