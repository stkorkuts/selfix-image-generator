using System.Collections.Immutable;
using LanguageExt;
using Selfix.ImageGenerator.Application.Abstractions;

namespace Selfix.ImageGenerator.Infrastructure;

internal sealed class DirectoryService : IDirectoryService
{
    public IO<DirectoryInfo> EnsureDirectory(string path) =>
        IO.lift(() => Directory.CreateDirectory(path));
    
    public IO<Unit> Delete(string path, bool recursive = false) =>
        IO.lift(() => Directory.Delete(path, recursive));
    
    public IO<IEnumerable<string>> EnumerateFiles(string path, string searchPattern) =>
        IO.lift(() => Directory.EnumerateFiles(path, searchPattern));
    
    public IO<ImmutableArray<string>> GetFiles(string path, string searchPattern) =>
        IO.lift(() => Directory.GetFiles(path, searchPattern).ToImmutableArray());
    
    public IO<Unit> Clean(string path) =>
        IO.lift(() =>
        {
            if(!Directory.Exists(path)) throw new DirectoryNotFoundException($"Directory {path} does not found");
            Directory.Delete(path, true);
            Directory.CreateDirectory(path);
        });
}