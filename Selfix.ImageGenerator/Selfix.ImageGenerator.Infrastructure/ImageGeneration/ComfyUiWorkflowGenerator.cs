using System.Collections.Immutable;
using LanguageExt;
using Microsoft.Extensions.Options;
using Selfix.ImageGenerator.Application.Abstractions;
using Selfix.ImageGenerator.Application.Model;
using Selfix.ImageGenerator.Infrastructure.ImageGeneration.Schema;
using Selfix.ImageGenerator.Shared.Extensions;
using Selfix.ImageGenerator.Shared.Settings;
using Selfix.Schema.Kafka;

namespace Selfix.ImageGenerator.Infrastructure.ImageGeneration;

internal sealed class ComfyUiWorkflowGenerator : IWorkflowGenerator
{
    private const string WorkflowTemplatePath = "prod/prod-api.json";

    private readonly IJsonSerializer _jsonSerializer;
    private readonly IFileService _fileService;
    private readonly EnvironmentSettings _environmentSettings;

    public ComfyUiWorkflowGenerator(
        IJsonSerializer jsonSerializer,
        IFileService fileService,
        IOptions<EnvironmentSettings> environmentSettings)
    {
        _jsonSerializer = jsonSerializer;
        _fileService = fileService;
        _environmentSettings = environmentSettings.Value;
    }

    public IO<ImmutableDictionary<string, WorkflowPromptItem>> Generate(GenerateWorkflowRequest request,
        CancellationToken cancellationToken) =>
        from templatePath in IO.pure(Path.Combine(_environmentSettings.WorkflowsDir, WorkflowTemplatePath))
        from fileStream in _fileService.OpenRead(templatePath)
        from template in _jsonSerializer
            .Deserialize<ImmutableDictionary<string, WorkflowPromptItem>>(fileStream, cancellationToken)
            .ToIOOrFail("Failed to deserialize workflow template")
        from _1 in ApplyRequestToTemplate(template, request).ToIO()
        from _2 in fileStream.DisposeAsyncIO()
        select template;

    private Fin<Unit> ApplyRequestToTemplate(ImmutableDictionary<string, WorkflowPromptItem> template,
        GenerateWorkflowRequest request)
    {
        return template.AsIterable().TraverseM(val => ProcessPromptItem(val.Key, val.Value, request)).IgnoreF().As();
    }

    private static Fin<Unit> ProcessPromptItem(string id, WorkflowPromptItem item, GenerateWorkflowRequest request)
    {
        switch (id)
        {
            case "2":
                item.Inputs["text"] = request.Prompt;
                return Unit.Default;
            case "37":
                item.Inputs["text"] = "GNAVTRTKN face of the person";
                return Unit.Default;
            case "3" or "67" or "76":
                item.Inputs["seed"] = request.Seed;
                return Unit.Default;
            case "5":
                item.Inputs["batch_size"] = request.Quantity;
                item.Inputs["width"] = request.AspectRatio switch
                {
                    ImageAspectRatio.Square1X1 => 1024,
                    ImageAspectRatio.Landscape16X9 => 1536,
                    ImageAspectRatio.Portrait9X16 => 864,
                    _ => 1024
                };
                item.Inputs["height"] = request.AspectRatio switch
                {
                    ImageAspectRatio.Square1X1 => 1024,
                    ImageAspectRatio.Landscape16X9 => 864,
                    ImageAspectRatio.Portrait9X16 => 1536,
                    _ => 1024
                };
                return Unit.Default;
            case "105" or "127":
                item.Inputs["lora_name"] = Path.GetFileName(request.LoraPath);
                return Unit.Default;
            default:
                return Unit.Default;
        }
    }
}