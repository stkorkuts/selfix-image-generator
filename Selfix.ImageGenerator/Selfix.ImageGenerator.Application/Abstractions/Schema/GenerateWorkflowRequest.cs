using Selfix.Schema.Kafka;

namespace Selfix.ImageGenerator.Infrastructure.ImageGeneration.Schema;

public sealed record GenerateWorkflowRequest(string Prompt, long Seed, string LoraPath, int Quantity, ImageAspectRatio AspectRatio);