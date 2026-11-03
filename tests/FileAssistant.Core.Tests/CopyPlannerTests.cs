using FileAssistant.Core.Features.Organize;

namespace FileAssistant.Core.Tests;

public sealed class CopyPlannerTests
{
    private static CopySource Source(string path, string identity) => new(path, "", "", false, new(10, 1, identity));
    [Fact]
    public void Batch_conflicts_require_a_decision_and_cannot_overwrite_another_batch_item()
    {
        var plan = CopyPlanner.Create(new([@"C:\a\one.txt", @"C:\b\one.txt"], @"D:\out", CopyLayout.Flat, CopyConflictPolicy.Confirm),
            [Source(@"C:\a\one.txt", "a"), Source(@"C:\b\one.txt", "b")], _ => null);
        var pending = Assert.Single(plan.Items, x => x.NeedsDecision);
        Assert.True(pending.BatchConflict); Assert.False(pending.CanOverwrite);
        Assert.Throws<InvalidOperationException>(() => CopyPlanner.Decide(plan, pending.Id, CopyDecision.Overwrite, _ => null));
        var decided = CopyPlanner.Decide(plan, pending.Id, CopyDecision.Number, _ => null);
        Assert.Equal(0, decided.PendingCount);
        Assert.Equal(@"D:\out\one (1).txt", decided.Items[1].Target);
    }
    [Fact]
    public void A_target_matching_another_input_is_protected_even_when_overwrite_is_selected()
    {
        var a = Source(@"C:\a\one.txt", "a"); var b = Source(@"D:\out\one.txt", "b");
        var plan = CopyPlanner.Create(new([a.Path, b.Path], @"D:\out", CopyLayout.Flat, CopyConflictPolicy.Confirm), [a, b],
            p => p == b.Path ? new(false, b.Stamp) : null);
        Assert.Equal(CopyAction.Rejected, plan.Items[0].Action); Assert.Equal(CopyAction.Skip, plan.Items[1].Action);
    }
    [Fact]
    public void Unreadable_sources_keep_the_expected_target_for_retry()
    {
        var source = Source(@"C:\a\one.txt", "a") with { Error = "missing" };
        var plan = CopyPlanner.Create(new([source.Path], @"D:\out", CopyLayout.Extension), [source], _ => null);
        Assert.Equal(@"D:\out\TXT\one.txt", Assert.Single(plan.Items).Target);
        Assert.Equal(CopyAction.Rejected, plan.Items[0].Action);
    }
    [Fact]
    public void File_blocking_a_parent_directory_is_rejected()
    {
        var source = Source(@"C:\a\one.txt", "a");
        var plan = CopyPlanner.Create(new([source.Path], @"D:\out", CopyLayout.Extension), [source],
            p => p == @"D:\out\TXT" ? new(false, new(1, 1, "blocker")) : null);
        Assert.Equal(CopyAction.Rejected, Assert.Single(plan.Items).Action);
    }
    [Theory]
    [InlineData(@"D:\out\a", @"D:\out", true)]
    [InlineData(@"D:\outside", @"D:\out", false)]
    [InlineData(@"D:\OUT\a", @"D:\out", true)]
    public void Directory_containment_is_segment_based(string path, string root, bool within) => Assert.Equal(within, CopyPlanner.Within(path, root));
    [Fact]
    public void Auto_numbering_rejects_overlong_names_instead_of_searching_forever()
    {
        var source = Source(@"C:\a\" + new string('a', 250) + ".txt", "a");
        var plan = CopyPlanner.Create(new([source.Path], @"D:\out", CopyLayout.Flat), [source],
            path => path == Path.Combine(@"D:\out", Path.GetFileName(source.Path)) ? new(false, new(1, 1, "old")) : null);
        Assert.Equal(CopyAction.Rejected, Assert.Single(plan.Items).Action);
    }
}
