using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FileAssistant.Core.Features.Search;

/// <summary>The supported query subset is shared by indexed, folder and batch search.</summary>
public static class SearchRules
{
    public static string Extensions(SearchKind kind) => kind switch
    {
        SearchKind.Images => "jpg;jpeg;png;webp;gif;bmp;tif;tiff;heic;avif;svg",
        SearchKind.Documents => "pdf;doc;docx;xls;xlsx;ppt;pptx;txt;md;csv;rtf;log",
        SearchKind.Archives => "zip;7z;rar;tar;gz;bz2;xz",
        SearchKind.Video => "mp4;mov;mkv;avi;wmv;webm;m4v",
        SearchKind.Audio => "mp3;wav;flac;aac;m4a;ogg;wma",
        SearchKind.Executables => "exe;msi;com;bat;cmd",
        _ => string.Empty
    };

    public static CompiledSearch Compile(SearchRequest request, DateTimeOffset? now = null)
    {
        if (request.Text.Length > 2048) throw new SearchException("搜索内容过长，请缩短到 2048 个字符以内。");
        List<List<SearchTerm>> groups = [[]];
        foreach (var token in Tokenize(request.Text))
        {
            if (token == "|")
            {
                if (groups[^1].Count == 0) throw new SearchException("“|” 两侧都需要搜索条件。");
                groups.Add([]);
            }
            else groups[^1].Add(ParseTerm(token, request.MatchPath, now ?? DateTimeOffset.Now));
        }
        if (groups.Count > 1 && groups[^1].Count == 0) throw new SearchException("“|” 后缺少搜索条件。");
        var filters = new List<SearchTerm>();
        var extensions = Extensions(request.Kind);
        if (extensions.Length > 0) filters.Add(ParseTerm("ext:" + extensions, false, now ?? DateTimeOffset.Now));
        if (request.Kind is SearchKind.Folders or SearchKind.Files)
            filters.Add(ParseTerm(request.Kind == SearchKind.Folders ? "folder:" : "file:", false, now ?? DateTimeOffset.Now));
        if (request.MinimumSize is { } min) filters.Add(new(f => !f.IsFolder && f.Size >= min, $"file: size:>={min}"));
        if (request.MaximumSize is { } max) filters.Add(new(f => !f.IsFolder && f.Size <= max, $"file: size:<={max}"));
        if (request.ModifiedFrom is { } date)
            filters.Add(new(f => f.Modified >= date, $"dm:>={date.LocalDateTime:yyyy-MM-dd}"));
        return new(groups, filters);
    }

    public static SearchPage Filter(IReadOnlyList<SearchFile> files, SearchRequest request, string source, int skipped = 0,
        CancellationToken cancellationToken = default)
    {
        var expression = Compile(request);
        var matches = new List<SearchFile>();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (expression.Matches(file)) matches.Add(file);
        }
        // Sort the complete result before paging, with a stable path tie-breaker.
        IOrderedEnumerable<SearchFile> ordered = request.Sort switch
        {
            SearchSort.Path => Order(matches, f => f.Directory, request.Descending, StringComparer.OrdinalIgnoreCase),
            SearchSort.Size => Order(matches, f => f.Size, request.Descending),
            SearchSort.Modified => Order(matches, f => f.Modified, request.Descending),
            _ => Order(matches, f => f.Name, request.Descending, StringComparer.OrdinalIgnoreCase)
        };
        var page = ordered.ThenBy(f => f.FullPath, StringComparer.OrdinalIgnoreCase)
            .Skip(request.Offset).Take(request.PageSize).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new(page, matches.Count, source, skipped);
    }

    private static IOrderedEnumerable<SearchFile> Order<T>(IEnumerable<SearchFile> files, Func<SearchFile, T> key,
        bool descending, IComparer<T>? comparer = null) => descending ? files.OrderByDescending(key, comparer) : files.OrderBy(key, comparer);

    private static IEnumerable<string> Tokenize(string text)
    {
        var token = new StringBuilder();
        var quoted = false;
        foreach (var c in text)
        {
            if (c == '"') { quoted = !quoted; token.Append(c); }
            else if (!quoted && (char.IsWhiteSpace(c) || c == '|'))
            {
                if (token.Length > 0) { yield return token.ToString(); token.Clear(); }
                if (c == '|') yield return "|";
            }
            else token.Append(c);
        }
        if (quoted) throw new SearchException("引号尚未闭合，请补上右侧双引号。");
        if (token.Length > 0) yield return token.ToString();
    }

    private static SearchTerm ParseTerm(string token, bool matchPath, DateTimeOffset now)
    {
        if (token.StartsWith('!'))
        {
            if (token.Length == 1) throw new SearchException("“!” 后需要排除的名称或条件。");
            var inner = ParseTerm(token[1..], matchPath, now);
            return new(f => !inner.Matches(f), "!<" + inner.Expression + ">");
        }
        var colon = token.IndexOf(':');
        if (colon > 1 && !token.StartsWith('"'))
        {
            var name = token[..colon].ToLowerInvariant();
            var value = token[(colon + 1)..].Trim('"');
            switch (name)
            {
                case "ext":
                    var exts = value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(e => e.TrimStart('.').ToLowerInvariant()).ToHashSet();
                    if (exts.Count == 0 || exts.Any(e => !Regex.IsMatch(e, "^[a-z0-9]+$"))) throw new SearchException("扩展名请使用 ext:jpg;png 这样的格式。");
                    return new(f => !f.IsFolder && exts.Contains(f.Extension), "ext:" + string.Join(';', exts));
                case "file" when value.Length == 0: return new(f => !f.IsFolder, "file:");
                case "folder" when value.Length == 0: return new(f => f.IsFolder, "folder:");
                case "size":
                    var size = Regex.Match(value, @"^(>=|<=|>|<|=)?(\d+(?:\.\d+)?)(b|kb|mb|gb)?$", RegexOptions.IgnoreCase);
                    if (!size.Success) throw new SearchException("大小条件请使用 size:>100mb，例如 size:<=2gb。");
                    if (!decimal.TryParse(size.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)) throw new SearchException("大小数值过大。");
                    var unit = size.Groups[3].Value.ToLowerInvariant();
                    var factor = unit switch { "kb" => 1024L, "mb" => 1048576L, "gb" => 1073741824L, _ => 1L };
                    if (amount > long.MaxValue / factor) throw new SearchException("大小数值过大。");
                    var bytes = (long)(amount * factor);
                    var op = size.Groups[1].Value;
                    return new(f => !f.IsFolder && f.Size is { } s && (op switch { ">" => s > bytes, ">=" => s >= bytes, "<" => s < bytes, "<=" => s <= bytes, _ => s == bytes }), $"file: size:{op}{bytes}");
                case "dm":
                    var today = new DateTimeOffset(now.LocalDateTime.Date, now.Offset);
                    DateTimeOffset from, to;
                    if (value.Equals("today", StringComparison.OrdinalIgnoreCase)) { from = today; to = today.AddDays(1); }
                    else if (value.Equals("7days", StringComparison.OrdinalIgnoreCase)) { from = today.AddDays(-6); to = today.AddDays(1); }
                    else if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) { from = new(day, now.Offset); to = from.AddDays(1); }
                    else throw new SearchException("日期条件支持 dm:today、dm:7days 或 dm:2026-10-30。");
                    return new(f => f.Modified >= from && f.Modified < to, $"dm:>={from:yyyy-MM-dd} dm:<{to:yyyy-MM-dd}");
                default: throw new SearchException($"暂不支持“{name}:”。可用 ext:、size:、dm:、file:、folder:，或给名称加双引号。");
            }
        }
        var text = token.Replace("\"", string.Empty);
        if (text.Length == 0) throw new SearchException("引号内需要填写名称。");
        var usePath = matchPath || text.Contains('\\') || text.Contains('/');
        Regex? wildcard = text.IndexOfAny(['*', '?']) >= 0
            ? new("^" + Regex.Escape(text).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking) : null;
        return new(f => wildcard?.IsMatch(usePath ? f.FullPath : f.Name) ?? (usePath ? f.FullPath : f.Name).Contains(text, StringComparison.OrdinalIgnoreCase),
            (usePath ? "path:" : "") + "\"" + text + "\"");
    }
}

public sealed record SearchTerm(Func<SearchFile, bool> Matches, string Expression);

public sealed class CompiledSearch(List<List<SearchTerm>> groups, List<SearchTerm> filters)
{
    public bool Matches(SearchFile file) => groups.Any(group => group.All(term => term.Matches(file))) && filters.All(term => term.Matches(file));
    public string EverythingExpression => string.Join(' ', new[]
    {
        groups.All(g => g.Count == 0) ? string.Empty : "<" + string.Join(" | ", groups.Select(group => string.Join(' ', group.Select(t => t.Expression)))) + ">",
        string.Join(' ', filters.Select(t => t.Expression))
    }.Where(s => s.Length > 0));
}
