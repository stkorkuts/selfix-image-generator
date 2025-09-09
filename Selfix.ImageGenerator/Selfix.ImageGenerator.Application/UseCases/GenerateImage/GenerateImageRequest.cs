using Selfix.Schema.Kafka;

namespace Selfix.ImageGenerator.Application.UseCases.GenerateImage;

public sealed record GenerateImageRequest(
    string JobId,
    string AvatarLoraPath,
    int Quantity,
    long Seed,
    string Prompt,
    ImageAspectRatio AspectRatio);