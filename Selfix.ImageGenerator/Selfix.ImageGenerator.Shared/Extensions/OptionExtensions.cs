using System.Diagnostics.CodeAnalysis;
using LanguageExt;
using LanguageExt.Common;

namespace Selfix.ImageGenerator.Shared.Extensions;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class OptionExtensions
{
    public static IO<T> ToIO<T>(this Option<T> option, Func<IO<T>> ifNone) =>
        option.Match(Some: IO.pure, None: ifNone);

    public static IO<T> ToIOOrFail<T>(this Option<T> option, Error error) =>
        option.ToIO(() => IO.fail<T>(error));
    
    public static IO<T> ToIOOrFail<T>(this Option<T> option, string errorMessage) =>
        option.ToIOOrFail(Error.New(errorMessage));
}