namespace Selfix.ImageGenerator.Shared.Settings;

public sealed class S3Settings
{
    public required string Endpoint { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    public required string Region { get; init; }
    public required string AvatarsBucketName { get; init; }
    public required string ResultImagesBucketName { get; init; }
}