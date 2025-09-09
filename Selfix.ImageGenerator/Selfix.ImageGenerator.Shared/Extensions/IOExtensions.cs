using System.Diagnostics.CodeAnalysis;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;

namespace Selfix.ImageGenerator.Shared.Extensions;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("Naming", "CA1715:Идентификаторы должны иметь правильные префиксы")]
public static class IOExtensions
{
    public static IO<T> ToIOOrFail<T>(this OptionT<IO, T> transformer, Error error) =>
        transformer.Run().Bind(option => option.ToIOOrFail(error)).As();

    public static IO<T> ToIOOrFail<T>(this OptionT<IO, T> transformer, string errorMessage) =>
        transformer.ToIOOrFail(Error.New(errorMessage));
    
    public static IO<A> TapOnFail<A, B>(this IO<A> io, Func<Error, IO<B>> func) =>
        io.IfFail(error => func(error)
            .Bind(_ => IO.fail<A>(error))
            .IfFail(innerError => IO.fail<A>(innerError + error)));
    
    public static IO<Unit> IgnoreF<T>(this IO<T> io) => io.Kind().IgnoreF().As();
    
    public static IO<T> WithLogging<T>(this IO<T> io, Action? before, Action? onSuccess, Action<Error>? onError) =>
        from _1 in IO.lift(() => before?.Invoke())
        from result in io.TapOnFail(err => IO.lift(() => onError?.Invoke(err)))
        from _2 in IO.lift(() => onSuccess?.Invoke())
        select result;
}