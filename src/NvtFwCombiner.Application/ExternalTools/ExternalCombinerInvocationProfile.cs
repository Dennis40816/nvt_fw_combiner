namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Closed command contract for one profile-selected legacy Combiner invocation.</summary>
public sealed class ExternalCombinerInvocationProfile
{

    /// <summary>Creates a profile-selected invocation that is separate from the tool package manifest.</summary>
    public ExternalCombinerInvocationProfile(
        string processorId,
        string toolBindingId,
        string inputMode,
        IEnumerable<string> argumentTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolBindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputMode);
        ArgumentNullException.ThrowIfNull(argumentTemplate);

        string[] argumentTemplateSnapshot = [.. argumentTemplate];
        if (argumentTemplateSnapshot.Length == 0)
        {
            throw new ArgumentException("External Combiner invocation must declare non-empty arguments.", nameof(argumentTemplate));
        }

        IReadOnlyList<string> argumentErrors = ExternalCombinerStagingTokens.FindArgumentTemplateErrors(argumentTemplateSnapshot);
        if (argumentErrors.Count > 0)
        {
            throw new ArgumentException(
                $"External Combiner invocation has invalid argument template: {string.Join(" ", argumentErrors)}",
                nameof(argumentTemplate));
        }

        IReadOnlyList<string> templateErrors = ExternalCombinerStagingTokens.FindArgumentTemplateErrors(argumentTemplateSnapshot);
        if (templateErrors.Count > 0)
        {
            throw new ArgumentException(
                $"External Combiner invocation declares an invalid argument template: {string.Join(" ", templateErrors)}",
                nameof(argumentTemplate));
        }

        ArgumentTemplate = Array.AsReadOnly(argumentTemplateSnapshot);
        ProcessorId = processorId;
        ToolBindingId = toolBindingId;
        InputMode = inputMode;
    }

    /// <summary>Profile processor id that selects this invocation.</summary>
    public string ProcessorId { get; }

    /// <summary>Approved tool binding required by this invocation.</summary>
    public string ToolBindingId { get; }

    /// <summary>Host staging input/output mode for this invocation.</summary>
    public string InputMode { get; }

    /// <summary>Host-expanded arguments in exact executable order.</summary>
    public IReadOnlyList<string> ArgumentTemplate { get; }
}
