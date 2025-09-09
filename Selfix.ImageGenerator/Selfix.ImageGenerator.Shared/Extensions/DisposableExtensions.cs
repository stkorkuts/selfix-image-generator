using System.Diagnostics.CodeAnalysis;
using LanguageExt;

namespace Selfix.ImageGenerator.Shared.Extensions;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class DisposableExtensions
{
    public static IO<Unit> DisposeAsyncIO(this IAsyncDisposable disposable) =>
        IO.liftVAsync(() => disposable.DisposeAsync().ToUnit());
    
    public static IO<Unit> DisposeIO(this IDisposable disposable) =>
        IO.lift(disposable.Dispose);
}