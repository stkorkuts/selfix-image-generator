using Microsoft.Extensions.Hosting;
using Selfix.ImageGenerator.Application.Abstractions;

namespace Selfix.ImageGenerator.EntryPoint.HostedServices;

internal sealed class ImageGeneratorHostedService : IHostedService
{
    private readonly IImageGenerator _imageGenerator;

    public ImageGeneratorHostedService(IImageGenerator imageGenerator) =>
        _imageGenerator = imageGenerator;

    public Task StartAsync(CancellationToken cancellationToken) =>
        _imageGenerator.Start(cancellationToken).RunAsync().AsTask();

    public Task StopAsync(CancellationToken cancellationToken) =>
        _imageGenerator.Stop(cancellationToken).RunAsync().AsTask();
}