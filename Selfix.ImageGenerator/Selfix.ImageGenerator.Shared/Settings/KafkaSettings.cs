namespace Selfix.ImageGenerator.Shared.Settings;

public sealed class KafkaSettings
{
    public required string BootstrapServer { get; init; }
    public required string SecurityProtocol { get; init; }
    public required SaslSettings Sasl { get; init; }
    public required string GroupId { get; init; }
    public required string TopicInput { get; init; }
    public required string TopicOutput { get; init; }
}

public sealed class SaslSettings
{
    public required string KafkaSecurityProtocol { get; init; }
    public required string KafkaSaslMechanism { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
}