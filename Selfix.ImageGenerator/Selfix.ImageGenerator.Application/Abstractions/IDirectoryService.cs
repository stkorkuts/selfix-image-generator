using System.Collections.Immutable;
using LanguageExt;

namespace Selfix.ImageGenerator.Application.Abstractions;

public interface IDirectoryService
{
    IO<DirectoryInfo> EnsureDirectory(string path);
    IO<Unit> Delete(string path, bool recursive = false);
    IO<IEnumerable<string>> EnumerateFiles(string path, string searchPattern);
    IO<ImmutableArray<string>> GetFiles(string path, string searchPattern);
    IO<Unit> Clean(string path);
}