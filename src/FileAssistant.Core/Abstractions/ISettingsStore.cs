namespace FileAssistant.Core.Abstractions;

/// <summary>Stores one settings document; its schema belongs to the consuming feature.</summary>
public interface ISettingsStore
{
    Task<string?> ReadAsync();
    Task WriteAsync(string contents);
}
