using LanguageExt;

namespace Selfix.ImageGenerator.Application.Abstractions;

public interface IResourceCleaner
{
    public IO<Unit> Cleanup(Option<string> loraPath, CancellationToken cancellationToken);
}