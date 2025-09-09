using System.Collections.Immutable;
using LanguageExt;
using Selfix.ImageGenerator.Application.Model;

namespace Selfix.ImageGenerator.Application.Abstractions;

public interface IImageGenerator
{
    IO<Unit> Start(CancellationToken cancellationToken);
    IO<Unit> Stop(CancellationToken cancellationToken);
    IO<Unit> Generate(ImmutableDictionary<string, WorkflowPromptItem> workflowPrompt, CancellationToken cancellationToken);
    IO<Unit> Cleanup(CancellationToken cancellationToken);
}