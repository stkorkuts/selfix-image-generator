using System.Diagnostics.CodeAnalysis;
using LanguageExt;

namespace Selfix.ImageGenerator.Shared.Extensions;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class TaskCompletionSourceExtensions
{
    public static IO<Unit> WaitForCompletion(this TaskCompletionSource taskCompletionSource,
        CancellationToken cancellationToken) =>
        IO.liftAsync(() => taskCompletionSource.Task.WaitAsync(cancellationToken).ToUnit());

    public static IO<bool> TrySetExceptionIO(this TaskCompletionSource source, Exception exception) =>
        IO.lift(() => source.TrySetException(exception));
}