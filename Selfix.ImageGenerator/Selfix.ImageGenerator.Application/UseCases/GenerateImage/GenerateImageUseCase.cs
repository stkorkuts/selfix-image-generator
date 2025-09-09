using LanguageExt;
using Microsoft.Extensions.Options;
using Selfix.ImageGenerator.Application.Abstractions;
using Selfix.ImageGenerator.Infrastructure.ImageGeneration.Schema;
using Selfix.ImageGenerator.Shared.Extensions;
using Selfix.ImageGenerator.Shared.Settings;
using Serilog;

namespace Selfix.ImageGenerator.Application.UseCases.GenerateImage;

public sealed class GenerateImageUseCase : IUseCase<GenerateImageRequest, GenerateImageResponse>
{
    private readonly IImageGenerator _imageGenerator;
    private readonly IWorkflowGenerator _workflowGenerator;
    private readonly IDirectoryService _directoryService;
    private readonly IObjectStorage _objectStorage;
    private readonly IResourceCleaner _resourceCleaner;
    private readonly IFileService _fileService;
    private readonly S3Settings _s3Settings;
    private readonly EnvironmentSettings _environmentSettings;

    public GenerateImageUseCase(
        IImageGenerator imageGenerator,
        IWorkflowGenerator workflowGenerator,
        IDirectoryService directoryService,
        IObjectStorage objectStorage,
        IResourceCleaner resourceCleaner,
        IFileService fileService,
        IOptions<S3Settings> s3Settings,
        IOptions<EnvironmentSettings> environmentSettings)
    {
        _imageGenerator = imageGenerator;
        _workflowGenerator = workflowGenerator;
        _directoryService = directoryService;
        _objectStorage = objectStorage;
        _resourceCleaner = resourceCleaner;
        _fileService = fileService;
        _s3Settings = s3Settings.Value;
        _environmentSettings = environmentSettings.Value;
    }

    public IO<GenerateImageResponse> Execute(GenerateImageRequest request, CancellationToken cancellationToken) =>
        from _1 in _resourceCleaner.Cleanup(Option<string>.None,  cancellationToken).WithLogging(
            () => Log.Information("Start cleaning up resources"),
            () => Log.Information("Resources cleaned up successfully"),
            err => Log.Error(err, "Resource cleanup failed"))
        from localLoraPath in DownloadLoraAvatar(_s3Settings.AvatarsBucketName, request.AvatarLoraPath, cancellationToken)
            .WithLogging(
                () => Log.Information("Start downloading lora avatar from {BucketName}, key: {Key}",
                    _s3Settings.AvatarsBucketName, request.AvatarLoraPath),
                () => Log.Information("Lora avatar downloaded successfully"),
                err => Log.Error(err, "Failed to download lora avatar"))
        from response in (
            from workflowPrompt in _workflowGenerator
                .Generate(new GenerateWorkflowRequest(request.Prompt, request.Seed, localLoraPath, request.Quantity, request.AspectRatio), cancellationToken)
                .WithLogging(
                    () => Log.Information("Start generating workflow prompt with seed: {Seed}", request.Seed),
                    () => Log.Information("Workflow prompt generated successfully"),
                    err => Log.Error(err, "Failed to generate workflow prompt"))
            from _3 in _imageGenerator.Generate(workflowPrompt, cancellationToken).WithLogging(
                () => Log.Information("Start generating image"),
                () => Log.Information("Image generated successfully"),
                err => Log.Error(err, "Failed to generate image"))
            from imagesKeys in UploadGeneratedImages(request.JobId, cancellationToken).WithLogging(
                () => Log.Information("Start uploading generated images for job {JobId}", request.JobId),
                () => Log.Information("Generated images uploaded successfully"),
                err => Log.Error(err, "Failed to upload generated images"))
            from _4 in _resourceCleaner.Cleanup(localLoraPath, cancellationToken).WithLogging(
                () => Log.Information("Start final cleanup of resources"),
                () => Log.Information("Final resources cleanup completed successfully"),
                err => Log.Error(err, "Final resource cleanup failed"))
            select new GenerateImageResponse(imagesKeys))
            .TapOnFail(_ => _resourceCleaner.Cleanup(localLoraPath, cancellationToken).WithLogging(
                () => Log.Information("Start final cleanup of resources after error"),
                () => Log.Information("Final resources cleanup after error completed successfully"),
                cleanupErr => Log.Error(cleanupErr, "Final resource cleanup after fail also failed")))
        select response;

    private IO<string> DownloadLoraAvatar(string bucketName, string key, CancellationToken cancellationToken) =>
        from _ in IO.lift(() =>
            Log.Debug("Starting DownloadLoraAvatar with bucket: {Bucket}, key: {Key}", bucketName, key))
        from localLoraPath in IO.pure(BuildLoraFilePath(key))
        from _1 in IO.lift(() => Log.Information("Downloading lora avatar to local path: {LocalPath}", localLoraPath))
        from stream in _objectStorage.GetObject(bucketName, key, cancellationToken)
        from _2 in IO.lift(() => Log.Debug("Stream received from S3, size: {Size} bytes", stream.Length))
        from _3 in _fileService.WriteStreamToFile(localLoraPath, stream, cancellationToken)
        from _4 in IO.lift(() => Log.Information("Lora avatar file saved successfully: {LocalPath}", localLoraPath))
        from _5 in stream.DisposeAsyncIO()
        select localLoraPath;

    private IO<Iterable<string>> UploadGeneratedImages(string jobId, CancellationToken cancellationToken) =>
        from _ in IO.lift(() => Log.Debug("Starting UploadGeneratedImages for job: {JobId}", jobId))
        from outputDirectory in IO.pure(_environmentSettings.OutputDir)
        from _1 in IO.lift(() =>
            Log.Information("Scanning for generated images in directory: {OutputDir}", outputDirectory))
        from paths in _directoryService.GetFiles(outputDirectory, "*.png")
        let iterablePaths = paths.AsIterable()
        from _2 in IO.lift(() => Log.Information("Found {Count} images to upload", iterablePaths.Count()))
        let imagesKeys = BuildImagesKeys(iterablePaths, jobId)
        from _3 in IO.lift(() => Log.Debug("Generated image keys: {Keys}", string.Join(", ", imagesKeys)))
        from _4 in iterablePaths
            .Zip(imagesKeys)
            .Traverse(tuple => UploadImageToS3(tuple.First, tuple.Second, cancellationToken))
        from _5 in IO.lift(() => Log.Information("Successfully uploaded all {Count} images for job {JobId}",
            iterablePaths.Count(), jobId))
        select imagesKeys;

    private IO<Unit> UploadImageToS3(string path, string key, CancellationToken cancellationToken) =>
        from _ in IO.lift(() => Log.Debug("Starting UploadImageToS3 with path: {Path}, key: {Key}", path, key))
        from stream in _fileService.OpenRead(path)
        from _1 in IO.lift(() => Log.Debug("Read local file {Path}, size: {Size} bytes", path, stream.Length))
        from _2 in _objectStorage.PutObject(_s3Settings.ResultImagesBucketName, key, stream, cancellationToken)
        from _3 in IO.lift(() => Log.Information("Image uploaded to S3 bucket: {Bucket}, key: {Key}",
            _s3Settings.ResultImagesBucketName, key))
        from _4 in stream.DisposeAsyncIO()
        select _4;

    private static Iterable<string> BuildImagesKeys(Iterable<string> paths, string jobId) =>
        paths.Map(path => BuildGeneratedImageKey(jobId, Path.GetFileName(path)));

    private string BuildLoraFilePath(string loraModel)
    {
        Log.Debug("Building lora file path from model: {LoraModel}", loraModel);
        string fileName = Guid.NewGuid().ToString("N") + ".safetensors";
        string filePath = Path.Combine(_environmentSettings.LorasDir, fileName);
        Log.Debug("Built lora file path: {FilePath}", filePath);
        return filePath;
    }

    private static string BuildGeneratedImageKey(string jobId, string imageName)
    {
        Log.Debug("Building image key for job: {JobId}, image: {ImageName}", jobId, imageName);
        string key = $"jobs/{jobId}/{imageName}";
        Log.Debug("Built image key: {Key}", key);
        return key;
    }
}