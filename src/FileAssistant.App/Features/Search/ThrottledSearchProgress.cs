using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Features.Search;

/// <summary>Limit scan notifications before they enter the UI dispatcher queue.</summary>
public sealed class ThrottledSearchProgress(IProgress<SearchProgress> target, TimeProvider? clock = null) : IProgress<SearchProgress>
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private readonly object gate = new();
    private long? last;
    private string? lastMessage;

    public void Report(SearchProgress value)
    {
        lock (gate)
        {
            var now = clock.GetTimestamp();
            if (last is { } previous && lastMessage == value.Message && clock.GetElapsedTime(previous, now) < TimeSpan.FromMilliseconds(100)) return;
            last = now;
            lastMessage = value.Message;
            target.Report(value);
        }
    }
}
