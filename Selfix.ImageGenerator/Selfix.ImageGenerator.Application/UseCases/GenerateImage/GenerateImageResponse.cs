namespace Selfix.ImageGenerator.Application.UseCases.GenerateImage;

public sealed record GenerateImageResponse(IEnumerable<string> GeneratedImagesKeys);