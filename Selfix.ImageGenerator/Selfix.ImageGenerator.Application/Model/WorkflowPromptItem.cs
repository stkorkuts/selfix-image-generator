using System.Collections.Immutable;

namespace Selfix.ImageGenerator.Application.Model;

public sealed record WorkflowPromptItem(Dictionary<string, object> Inputs, string ClassType);