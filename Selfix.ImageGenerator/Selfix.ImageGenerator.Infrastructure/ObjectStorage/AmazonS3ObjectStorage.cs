using Amazon.S3;
using Amazon.S3.Model;
using LanguageExt;
using Selfix.ImageGenerator.Application.Abstractions;

namespace Selfix.ImageGenerator.Infrastructure.ObjectStorage;

internal sealed class AmazonS3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _client;

    public AmazonS3ObjectStorage(IAmazonS3 client) => _client = client;

    public IO<Stream> GetObject(string bucketName, string key, CancellationToken cancellationToken) =>
        IO.liftAsync(() => _client
            .GetObjectAsync(bucketName, key.TrimStart('/'), cancellationToken)
            .Map(response => response.ResponseStream));

    public IO<Unit> PutObject(string bucketName, string key, Stream stream, CancellationToken cancellationToken) =>
        IO.liftAsync(() => _client
            .PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key.TrimStart('/'),
                InputStream = stream
            }, cancellationToken)
            .ToUnit());
}