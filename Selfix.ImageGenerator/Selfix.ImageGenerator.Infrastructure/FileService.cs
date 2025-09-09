using LanguageExt;
using Selfix.ImageGenerator.Application.Abstractions;

namespace Selfix.ImageGenerator.Infrastructure;

internal sealed class FileService : IFileService
{
    public IO<Stream> Open(string path, FileMode mode, FileAccess access, FileShare share) =>
        IO.lift(Stream () => File.Open(path, mode, access, share));

    public IO<Unit> Delete(string path)
    {
        return IO.lift(() => File.Delete(path));
    }
}