namespace FileAssistant.Core.Engines;

/// <summary>Local preview and conversion boundary. No UI or third-party types cross this interface.</summary>
public interface IFileProcessingService
{
    IReadOnlyList<ProcessingEngineInfo> Engines { get; }
    Task<PreviewResult> PreviewAsync(string path, int maximumEdge, CancellationToken cancellationToken);
    Task<ConversionPlan> CreatePlanAsync(ConversionRequest request, CancellationToken cancellationToken);
    Task<ConversionReport> ConvertAsync(ConversionPlan plan, IProgress<ProcessingProgress>? progress, CancellationToken cancellationToken);
}
