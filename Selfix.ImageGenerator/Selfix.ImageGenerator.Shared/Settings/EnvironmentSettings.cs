namespace Selfix.ImageGenerator.Shared.Settings;

public sealed class EnvironmentSettings
{
    public required string LorasDir { get; init; }
    public required string WorkflowsDir { get; init; }
    public required string OutputDir { get; init; }
    public required bool IsHighVram { get; init; }
}