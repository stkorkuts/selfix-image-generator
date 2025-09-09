# ComfyService

```csharp
using System.Net.WebSockets;
using System.Text.Json;
using PoFoto.Designer.Infrastructure.ComfyUI.Models;
using PoFoto.Designer.Shared;
using PoFoto.Designer.Shared.AppSettings;
using PoFoto.Designer.Shared.Assets.ArtGallery;
using PoFoto.Designer.Shared.Assets.Upscaler;
using Websocket.Client;

namespace PoFoto.Designer.Infrastructure.ComfyUI;

public class ComfyService
{
    private readonly ComfySettings _comfySettings;
    private readonly Guid _clientId;
    private readonly WebsocketClient _wsClient;
    private readonly HttpClient _httpClient;

    private CancellationTokenSource? _cancellationTokenSource;

    private JobTypeEnum _currentJobType;
    private BookTypeEnum _currentBookType;

    private Exception? _currentJobException;
    private WorkflowResponse? _currentWorkflowResponse;

    public string ClientId => _clientId.ToString();

    public ComfyService(ComfySettings comfySettings)
    {
        _comfySettings = comfySettings;
        _clientId = Guid.NewGuid();
        _wsClient = new WebsocketClient(new Uri($"ws://localhost:8188/ws?clientId={_clientId}"));
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:8188")
        };
    }

    public async Task StartAsync()
    {
        _wsClient.MessageReceived.Subscribe(msg =>
        {
            HandleComfyUIMessage(msg.Text);
        });

        _wsClient.ReconnectionHappened.Subscribe(_ =>
        {
            Console.WriteLine("Reconnection happened.");
        });

        await _wsClient.Start();
    }

    public async Task StopAsync()
    {
        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
        }
        await _wsClient.Stop(WebSocketCloseStatus.NormalClosure, string.Empty);
    }

    public async Task<WorkflowResponse> Generate(WorkflowPromptRequest request, JobTypeEnum jobType, BookTypeEnum bookType)
    {
        if(_cancellationTokenSource is not null) throw new InvalidOperationException("Comfy service is busy.");
        if(!_comfySettings.WorkflowProcessedCheckIntervalMs.HasValue) throw new InvalidOperationException("Workflow check interval must be specified.");
        if(jobType is not (JobTypeEnum.Generate or JobTypeEnum.Upscale)) throw new InvalidOperationException("Invalid job type.");

        _cancellationTokenSource = new CancellationTokenSource();
        _currentJobException = null;
        _currentWorkflowResponse = null;

        _currentJobType = jobType;
        _currentBookType = bookType;

        request.ClientId = _clientId.ToString();

        var content = new StringContent(JsonSerializer.Serialize(request));
        var response = await _httpClient.PostAsync("/prompt", content);
        response.EnsureSuccessStatusCode();

        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_comfySettings.WorkflowProcessedCheckIntervalMs.Value, _cancellationTokenSource.Token);
            }
            catch (TaskCanceledException){}
        }

        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;

        if(_currentJobException is not null) throw _currentJobException;
        if(_currentWorkflowResponse is null) throw new NullReferenceException("Workflow response was not filled.");
        return _currentWorkflowResponse;
    }

    private void HandleComfyUIMessage(string? message)
    {
        if (message is null) return;

        using JsonDocument doc = JsonDocument.Parse(message);
        string type = doc.RootElement.GetProperty("type").GetString();
        if (type == "crystools.monitor") return;
        switch (type)
        {
            case "executed":
                HandleExecutedMessage(doc);
                break;
            case "execution_success":
                HandleExecutionSuccessMessage(doc);
                break;
            default:
                Console.WriteLine($"Received message type: {message}");
                break;
        }
    }

    private void HandleExecutedMessage(JsonDocument doc)
    {
        if (_currentWorkflowResponse is not null) return;

        var executedNodeId = doc.RootElement.GetProperty("data").GetProperty("node").GetString();
        if (_currentJobType is JobTypeEnum.Generate && _currentBookType == BookTypeEnum.ArtGallery &&
            executedNodeId == ArtGalleryConstants.IMAGES_OUTPUT_ELEMENT_ID)
        {
            _currentWorkflowResponse =
                GetResponseFromImagesArray(doc.RootElement.GetProperty("data").GetProperty("output")
                    .GetProperty("images"));
            return;
        }

        if (_currentJobType is JobTypeEnum.Upscale && executedNodeId == UpscalerConstants.IMAGES_OUTPUT_ELEMENT_ID)
        {
            _currentWorkflowResponse =
                GetResponseFromImagesArray(doc.RootElement.GetProperty("data").GetProperty("output")
                    .GetProperty("images"));
            return;
        }
    }

    private WorkflowResponse GetResponseFromImagesArray(JsonElement imagesElement)
    {
        var generatedFileNames = new List<string>();

        foreach (var file in imagesElement.EnumerateArray())
        {
            generatedFileNames.Add(file.GetProperty("filename").GetString()!);
        }

        return new WorkflowResponse
        {
            GeneratedFileNames = generatedFileNames,
        };
    }

    private void HandleExecutionSuccessMessage(JsonDocument doc)
    {
        _cancellationTokenSource?.Cancel();
    }
}
```

# WorkflowGenerator

```csharp
using System.Text.Json;
using PoFoto.Designer.Infrastructure.ComfyUI.Models;
using PoFoto.Designer.Infrastructure.Database.Entities;
using PoFoto.Designer.Shared.AppSettings;
using PoFoto.Designer.Shared.Assets.Upscaler;

namespace PoFoto.Designer.Infrastructure.ComfyUI;

public class UpscalerWorkflowPromptRequestGenerator
{
    private readonly UpscalerWorkflowPromptGeneratorConfiguration _config;
    private readonly EnvironmentSettings _envSettings;

    public UpscalerWorkflowPromptRequestGenerator(UpscalerWorkflowPromptGeneratorConfiguration config, EnvironmentSettings envSettings)
    {
        _config = config;
        _envSettings = envSettings;
    }

    public WorkflowPromptRequest Generate(UpscalerWorkflowPromptRequestSpecification specs)
    {
        if (string.IsNullOrWhiteSpace(_envSettings.WorkingDirectory))
            throw new Exception("Working directory name is required.");
        if (string.IsNullOrWhiteSpace(_envSettings.ValidatedDirectoryName))
            throw new Exception("Validated directory name is required.");
        if (string.IsNullOrWhiteSpace(_envSettings.UpscaledDirectoryName))
            throw new Exception("Upscaled directory name is required.");

        var requestTemplate = JsonSerializer.Deserialize<WorkflowPromptRequest>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "Upscaler", "upscale.json")));

        if (requestTemplate is null)
            throw new Exception("Upscale prompt request template is invalid.");

        var random = new Random();

        var inputDirectory =
            Path.Combine(_envSettings.WorkingDirectory, specs.Order.Id, _envSettings.ValidatedDirectoryName);
        var outputDirectory =
            Path.Combine(_envSettings.WorkingDirectory, specs.Order.Id, _envSettings.UpscaledDirectoryName);

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        requestTemplate.ClientId = specs.ClientId;
        requestTemplate.Prompt[UpscalerConstants.IMAGES_INPUT_ELEMENT_ID].Inputs["folder"] = inputDirectory;
        requestTemplate.Prompt[UpscalerConstants.IMAGES_OUTPUT_ELEMENT_ID].Inputs["output_folder"] = outputDirectory;
        requestTemplate.Prompt[UpscalerConstants.UPSCALER_MODEL_ELEMENT_ID].Inputs["seed"] = random.NextInt64();

        return requestTemplate;
    }
}

public record UpscalerWorkflowPromptRequestSpecification(string ClientId, OrderDb Order);

public record UpscalerWorkflowPromptGeneratorConfiguration();
```

# WorkflowPromptRequest

```csharp
using System.Text.Json.Serialization;

namespace PoFoto.Designer.Infrastructure.ComfyUI.Models;

public class WorkflowPromptRequest
{
    [JsonPropertyName("prompt")]
    public Dictionary<string, WorkflowPromptItem> Prompt { get; set; }

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; }
}

public class WorkflowPromptItem
{
    [JsonPropertyName("inputs")]
    public Dictionary<string, object> Inputs { get; set; }

    [JsonPropertyName("class_type")]
    public string ClassType { get; set; }
}
```
