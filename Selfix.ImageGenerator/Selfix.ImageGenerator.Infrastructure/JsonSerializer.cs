using System.Text.Json;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace Selfix.ImageGenerator.Infrastructure;

internal interface IJsonSerializer
{
    Option<T> Deserialize<T>(string json);
    Option<T> Deserialize<T>(Stream stream);
    OptionT<IO, T> Deserialize<T>(Stream stream, CancellationToken cancellationToken);
    string Serialize<T>(T value);
    IO<Unit> Serialize<T>(Stream stream, Option<T> value, CancellationToken cancellationToken);
}

internal sealed class SystemJsonSerializer(JsonSerializerOptions options) : IJsonSerializer
{
    public Option<T> Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, options);

    public Option<T> Deserialize<T>(Stream stream) => JsonSerializer.Deserialize<T>(stream, options);

    public OptionT<IO, T> Deserialize<T>(Stream stream, CancellationToken cancellationToken) =>
        IO.liftVAsync(() => JsonSerializer
            .DeserializeAsync<T>(stream, options, cancellationToken)
            .Map(Prelude.Optional));
    
    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, options);

    public IO<Unit> Serialize<T>(Stream stream, Option<T> value, CancellationToken cancellationToken) =>
        IO.liftAsync(() => JsonSerializer
            .SerializeAsync(stream, value.ValueUnsafe(), options, cancellationToken).ToUnit());
}