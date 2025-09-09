using LanguageExt;
using MassTransit;
using Microsoft.Extensions.Options;
using Selfix.ImageGenerator.Application.UseCases.GenerateImage;
using Selfix.ImageGenerator.Shared.Settings;
using Selfix.Schema.Kafka.Jobs.Images.V1.ImageGeneration;
using Serilog;

namespace Selfix.ImageGenerator.Infrastructure.EventStreaming;

internal sealed class ImageGenerationConsumer : IConsumer<GenerateImageRequestEvent>
{
    private readonly ITopicProducer<GenerateImageResponseEvent> _topicProducer;
    private readonly GenerateImageUseCase _useCase;

    public ImageGenerationConsumer(GenerateImageUseCase useCase, ITopicProducer<GenerateImageResponseEvent> topicProducer)
    {
        _topicProducer = topicProducer;
        _useCase = useCase;
    }

    public async Task Consume(ConsumeContext<GenerateImageRequestEvent> context)
    {
        GenerateImageRequestEvent message = context.Message;
        GenerateImageRequest request = new(message.JobId, message.AvatarLoraPath, message.Quantity, message.Seed,
            message.Prompt, message.AspectRatio);

        try
        {
            GenerateImageResponse response = await _useCase
                .Execute(request, context.CancellationToken)
                .RunAsync();
            
            await _topicProducer.Produce(new GenerateImageResponseEvent
            {
                JobId = message.JobId,
                Success = new GenerateImageResponseEventSuccessData
                {
                    GeneratedImagesPaths = response.GeneratedImagesKeys.ToArray()
                },
                IsSuccess = true
            }, context.CancellationToken);
        }
        catch(Exception ex)
        {
            Log.Error(ex, "Error while processing image generation request");
            await _topicProducer.Produce(new GenerateImageResponseEvent
            {
                JobId = message.JobId,
                Fail = new GenerateImageResponseEventFailData { Error = ex.ToString() },
                IsSuccess = false
            }, context.CancellationToken);
        }
    }
}