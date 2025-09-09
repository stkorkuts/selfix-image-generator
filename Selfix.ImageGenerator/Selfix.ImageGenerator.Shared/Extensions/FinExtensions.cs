using System.Diagnostics.CodeAnalysis;
using LanguageExt;

namespace Selfix.ImageGenerator.Shared.Extensions;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class FinExtensions
{
    public static IO<T> ToIO<T>(this Fin<T> fin) => fin.Match(Succ: IO<T>.Pure, Fail: IO<T>.Fail);
}