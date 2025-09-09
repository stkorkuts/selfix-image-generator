using System.Collections.Immutable;
using System.Net.WebSockets;
using LanguageExt;
using Microsoft.Extensions.Options;
using Selfix.ImageGenerator.Application.Abstractions;
using Selfix.ImageGenerator.Application.Model;
using Selfix.ImageGenerator.Shared.Extensions;
using Selfix.ImageGenerator.Shared.Settings;
using Serilog;
using Websocket.Client;

namespace Selfix.ImageGenerator.Infrastructure.ImageGeneration;

internal sealed class ComfyUiImageGenerator : IImageGenerator, IDisposable
{
    private const string ExecutionSuccess = "execution_success";
    private const string ExecutionFail = "execution_fail";
    private const string ExecutionError = "execution_error";
    private const string Executing = "executing";
    private const string Executed = "executed";
    private const string Progress = "progress";
    private const int MaxRetriesCount = 3;

    private readonly IJsonSerializer _jsonSerializer;
    private readonly Ulid _clientId;
    private readonly WebsocketClient _websocketClient;
    private readonly HttpClient _httpClient;

    private readonly Atom<Option<PendingComfyUiRequest>> _pendingRequest;

    public ComfyUiImageGenerator(IOptions<ComfySettings> options, IJsonSerializer jsonSerializer)
    {
        string host = options.Value.Host;
        int port = options.Value.Port;

        _jsonSerializer = jsonSerializer;
        _clientId = Ulid.NewUlid();
        _websocketClient = new WebsocketClient(new Uri($"ws://{host}:{port}/ws?clientId={_clientId}"));
        _httpClient = new HttpClient { BaseAddress = new Uri($"http://{host}:{port}") };
        _pendingRequest = Prelude.Atom(Option<PendingComfyUiRequest>.None);
        _websocketClient.MessageReceived.Subscribe(OnMessageReceived);
    }

    public void Dispose()
    {
        _websocketClient.Dispose();
        _httpClient.Dispose();
    }

    public IO<Unit> Start(CancellationToken cancellationToken) =>
        IO.liftAsync(() => _websocketClient
            .StartOrFail()
            .WaitAsync(cancellationToken)
            .ToUnit());

    public IO<Unit> Stop(CancellationToken cancellationToken) =>
        IO.liftAsync(() => _websocketClient
            .StopOrFail(WebSocketCloseStatus.NormalClosure, string.Empty)
            .WaitAsync(cancellationToken)
            .ToUnit());

    public IO<Unit> Generate(ImmutableDictionary<string, WorkflowPromptItem> workflowPrompt, CancellationToken cancellationToken) =>
        from optionPendingRequest in _pendingRequest.ValueIO
        from _1 in optionPendingRequest.Match(
            Some: _ => IO.fail<Unit>("Generation already in progress"),
            None: () => IO.pure(Unit.Default))
        let promptRequest = new WorkflowPromptRequest(workflowPrompt, _clientId.ToString())
        from _2 in SendPromptRequest(promptRequest, cancellationToken)
        let pendingRequest = PendingComfyUiRequest.New(promptRequest, cancellationToken)
        from _3 in SavePendingRequest(pendingRequest)
        from response in pendingRequest.TaskCompletionSource
            .WaitForCompletion(cancellationToken)
            .Finally(ClearPendingRequest())
        select response;

    public IO<Unit> Cleanup(CancellationToken cancellationToken) =>
        IO.liftAsync(() => _httpClient.PostAsync("/cleanup", null, cancellationToken).ToUnit());

    private async void OnMessageReceived(ResponseMessage response)
    {
        if (_pendingRequest.Value.IsNone) return;
        if (string.IsNullOrWhiteSpace(response.Text)) return;

        await TryHandleComfyUiMessage(response.Text)
            .IfFail(error =>
            {
                Log.Error(error, "Failed to handle ComfyUI message");
                return Unit.Default;
            })
            .RunAsync();
    }

    private IO<Unit> TryHandleComfyUiMessage(string message) =>
        from requestOption in _pendingRequest.ValueIO
        from _ in requestOption.Match(
            Some: request => HandleComfyUiMessage(message, request)
                .TapOnFail(error => request.TaskCompletionSource.TrySetExceptionIO(error)),
            None: () => IO.pure(Unit.Default))
        select _;
    
    private IO<Unit> HandleComfyUiMessage(string message, PendingComfyUiRequest request) =>
        from response in _jsonSerializer
            .Deserialize<ComfyUiResponse>(message)
            .ToIOOrFail("Failed to deserialize ComfyUI message")
        from _ in response.Type switch
        {
            ExecutionSuccess => HandleExecutionSuccess(request),
            ExecutionFail => AttemptGenerationRetry(request),
            ExecutionError => AttemptGenerationRetry(request),
            Executing => IO.pure(Unit.Default),
            Executed => IO.pure(Unit.Default),
            Progress => IO.pure(Unit.Default),
            _ => IO.lift(() => Log.Warning("Got unhandled ComfyUI message: {Message} with type: {Type}", message, response.Type))
        }
        select _;
    
    private IO<Unit> SendPromptRequest(WorkflowPromptRequest request, CancellationToken cancellationToken) =>
        from content in IO.lift(() => new StringContent(_jsonSerializer.Serialize(request)))
        from response in IO.liftAsync(() => _httpClient.PostAsync("/prompt", content, cancellationToken))
        from _1 in response.IsSuccessStatusCode
            ? IO.pure(Unit.Default)
            : IO.fail<Unit>(response.ToString())
        from _2 in content.DisposeIO()
        from _3 in response.DisposeIO()
        select _3;

    private IO<Unit> SavePendingRequest(PendingComfyUiRequest request) =>
        _pendingRequest.SwapIO(_ => request).IgnoreF();

    private IO<Unit> ClearPendingRequest() =>
        _pendingRequest.SwapIO(_ => Option<PendingComfyUiRequest>.None).IgnoreF();

    private static IO<Unit> HandleExecutionSuccess(PendingComfyUiRequest request) =>
        IO.lift(() => request.TaskCompletionSource.TrySetResult()).IgnoreF();

    private IO<Unit> AttemptGenerationRetry(PendingComfyUiRequest request) =>
        from retriesCount in request.RetriesCount.SwapIO(value => value + 1)
        from _ in retriesCount <= MaxRetriesCount
            ? SendPromptRequest(request.Request, request.CancellationToken).WithLogging(
                () => Log.Information("Attempting generation retry {RetryCount}/{MaxRetries}", retriesCount,
                    MaxRetriesCount),
                () => Log.Information("Successful generation retry {RetryCount}/{MaxRetries}", retriesCount,
                    MaxRetriesCount),
                error => Log.Error(error, "Failed generation retry {RetryCount}/{MaxRetries}", retriesCount,
                    MaxRetriesCount))
            : IO.fail<Unit>($"Max retries exceeded: {MaxRetriesCount}")
        select _;
    
    private sealed record WorkflowPromptRequest(
        ImmutableDictionary<string, WorkflowPromptItem> Prompt,
        string ClientId);

    private sealed record ComfyUiResponse(string Type);

    private sealed record PendingComfyUiRequest(
        WorkflowPromptRequest Request,
        TaskCompletionSource TaskCompletionSource,
        Atom<uint> RetriesCount,
        CancellationToken CancellationToken)
    {
        public static PendingComfyUiRequest New(WorkflowPromptRequest request, CancellationToken cancellationToken) =>
            new(request, new TaskCompletionSource(), Prelude.Atom(0u), cancellationToken);
    }
}