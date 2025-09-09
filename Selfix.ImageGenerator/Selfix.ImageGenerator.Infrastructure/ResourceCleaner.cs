using LanguageExt;
using Microsoft.Extensions.Options;
using Selfix.ImageGenerator.Application.Abstractions;
using Selfix.ImageGenerator.Shared.Settings;

namespace Selfix.ImageGenerator.Infrastructure;

internal sealed class ResourceCleaner : IResourceCleaner
{
    private readonly IImageGenerator _imageGenerator;
    private readonly IDirectoryService _directoryService;
    private readonly IFileService _fileService;
    private readonly EnvironmentSettings _environmentSettings;

    public ResourceCleaner(
        IImageGenerator imageGenerator,
        IOptions<EnvironmentSettings> environmentSettings,
        IDirectoryService directoryService,
        IFileService fileService)
    {
        _imageGenerator = imageGenerator;
        _directoryService = directoryService;
        _fileService = fileService;
        _environmentSettings = environmentSettings.Value;
    }

    public IO<Unit> Cleanup(Option<string> loraPath, CancellationToken cancellationToken) =>
        from _1 in _imageGenerator.Cleanup(cancellationToken)
        from _2 in loraPath.Match(path => _fileService.Delete(path), () => IO.pure(Unit.Default)) 
        from _3 in _directoryService.Clean(_environmentSettings.OutputDir)
        select Unit.Default;
}