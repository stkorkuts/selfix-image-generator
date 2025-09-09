using System.Collections.Immutable;
using LanguageExt;
using Selfix.ImageGenerator.Application.Model;
using Selfix.ImageGenerator.Infrastructure.ImageGeneration.Schema;

namespace Selfix.ImageGenerator.Application.Abstractions;

public interface IWorkflowGenerator
{
    IO<ImmutableDictionary<string, WorkflowPromptItem>> Generate(GenerateWorkflowRequest request, CancellationToken cancellationToken);
}