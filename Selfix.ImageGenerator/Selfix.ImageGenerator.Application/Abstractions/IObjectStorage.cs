using LanguageExt;

namespace Selfix.ImageGenerator.Application.Abstractions;

public interface IObjectStorage
{
    IO<Stream> GetObject(string bucketName, string key, CancellationToken cancellationToken);
    IO<Unit> PutObject(string bucketName, string key, Stream stream, CancellationToken cancellationToken);
}