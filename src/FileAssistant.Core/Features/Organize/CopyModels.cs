using FileAssistant.Core.Engines;

namespace FileAssistant.Core.Features.Organize;

public enum CopyLayout { Preserve, Flat, Extension }
public enum CopyConflictPolicy { Number, Skip, Confirm }
public enum CopyAction { Copy, Number, Overwrite, Skip, Rejected, Pending }
public enum CopyOutcome { Success, Skipped, Failed, Cancelled, NotStarted, Pending }
public enum CopyDecision { Number, Skip, Overwrite }
public sealed record CopyRequest(IReadOnlyList<string> Paths, string Destination, CopyLayout Layout = CopyLayout.Preserve,
    CopyConflictPolicy Conflict = CopyConflictPolicy.Number);
public sealed record FileStamp(long Length, long ModifiedUtcTicks, string Identity, string? Hash = null);
public sealed record CopySource(string Path, string Root, string Relative, bool IsDirectory, FileStamp? Stamp, string? Error = null);
public sealed record CopyTarget(bool IsDirectory, FileStamp? Stamp, string? Error = null);
public sealed record CopyPlanItem(Guid Id, CopySource Source, string Target, CopyAction Action,
    string? Message = null, FileStamp? ApprovedTarget = null, bool BatchConflict = false)
{
    public bool CanRun => Action is CopyAction.Copy or CopyAction.Number or CopyAction.Overwrite;
    public bool NeedsDecision => Action == CopyAction.Pending;
    public bool CanOverwrite => NeedsDecision && !BatchConflict && ApprovedTarget is not null;
    public string Status => Action switch
    {
        CopyAction.Copy => Source.IsDirectory ? "创建目录" : "待复制", CopyAction.Number => "自动编号",
        CopyAction.Overwrite => "覆盖 · 保留备份", CopyAction.Skip => "跳过", CopyAction.Rejected => "不可处理", _ => "待确认"
    };
    public string Description => $"{Status} · {System.IO.Path.GetFileName(Source.Path)}";
    public string Paths => $"{Source.Path}\n→ {Target}{(Message is null ? "" : "\n" + Message)}";
}
public sealed record CopyPlan(CopyRequest Request, IReadOnlyList<CopyPlanItem> Items)
{
    public int FileCount => Items.Count(x => !x.Source.IsDirectory);
    public long TotalBytes => Items.Where(x => !x.Source.IsDirectory).Sum(x => x.Source.Stamp?.Length ?? 0);
    public long CopyBytes => Items.Where(x => x.CanRun && !x.Source.IsDirectory).Sum(x => x.Source.Stamp?.Length ?? 0);
    public int PendingCount => Items.Count(x => x.NeedsDecision);
    public int ReadyCount => Items.Count(x => x.CanRun);
}
public sealed record CopyItemResult(CopyPlanItem Item, CopyOutcome Outcome, string Message, ProducedFile? Output = null)
{
    public bool Retryable => Outcome is CopyOutcome.Failed or CopyOutcome.Cancelled or CopyOutcome.NotStarted or CopyOutcome.Pending;
    public string Description => $"{Item.Source.Path}\n→ {Item.Target}\n{Message}";
}
public sealed record CopyReport(CopyPlan Plan, IReadOnlyList<CopyItemResult> Items, bool Cancelled, string? StopReason = null)
{
    public IReadOnlyList<ProducedFile> Outputs => Items.Where(x => x.Output is not null).Select(x => x.Output!).ToArray();
    public string Summary => $"成功 {Items.Count(x => x.Outcome == CopyOutcome.Success)} · 跳过 {Items.Count(x => x.Outcome == CopyOutcome.Skipped)} · 未完成 {Items.Count(x => x.Retryable)}" +
        (Cancelled ? " · 已取消" : "") + (StopReason is null ? "" : " · " + StopReason);
}
public sealed record CopyRecovery(Guid Id, string Target, string Backup, string Source, string NewHash, string OldHash,
    DateTimeOffset Created, string State, string? Message = null)
{
    public string Description => $"{Created.LocalDateTime:g} · {State}\n{Target}\n备份：{Backup}{(Message is null ? "" : "\n" + Message)}";
}
public interface ICopyService
{
    Task<CopyPlan> PlanAsync(CopyRequest request, IProgress<ProcessingProgress>? progress, CancellationToken token);
    Task<CopyPlan> DecideAsync(CopyPlan plan, Guid item, CopyDecision decision, CancellationToken token);
    Task<CopyReport> ExecuteAsync(CopyPlan plan, IProgress<ProcessingProgress>? progress, CancellationToken token);
    Task<CopyPlan> RetryPlanAsync(CopyReport report, CancellationToken token);
    Task<IReadOnlyList<CopyRecovery>> RecoveriesAsync(CancellationToken token);
    Task RestoreAsync(Guid recovery, CancellationToken token);
    Task DiscardBackupAsync(Guid recovery, CancellationToken token);
}
