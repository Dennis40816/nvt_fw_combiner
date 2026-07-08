using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Postbuild command profile for one IC family.</summary>
public sealed class LegacyCombinerPostbuildProfile
{
    private readonly LegacyCombinerPostbuildCommand[] _singleCommands;
    private readonly LegacyCombinerPostbuildCommand[] _cascadeCommands;
    private readonly LegacyCombinerPostbuildCommand[]? _cascadeExtendedCommands;
    private readonly LegacyCombinerPostbuildCommand[]? _twoChipCommands;
    private readonly LegacyCombinerPostbuildCommand[]? _threeChipCommands;
    private readonly Dictionary<string, LegacyCombinerPostbuildBranch> _branchRules;

    /// <summary>Creates a postbuild command profile.</summary>
    public LegacyCombinerPostbuildProfile(
        string processorId,
        string icId,
        string toolBindingId,
        string firmwareFileName,
        IEnumerable<LegacyCombinerPostbuildCommand> singleCommands,
        IEnumerable<LegacyCombinerPostbuildCommand> cascadeCommands,
        string evidence,
        IEnumerable<LegacyCombinerPostbuildCommand>? cascadeExtendedCommands = null,
        IEnumerable<LegacyCombinerPostbuildCommand>? twoChipCommands = null,
        IEnumerable<LegacyCombinerPostbuildCommand>? threeChipCommands = null,
        IEnumerable<LegacyCombinerPostbuildBranchRule>? branchRules = null,
        LegacyCombinerPostbuildAssemblyKind assemblyKind = LegacyCombinerPostbuildAssemblyKind.InPlaceFirmwareImage,
        LegacyCombinerCommonFwVersionRule? commonFwVersionRule = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolBindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(firmwareFileName);
        ArgumentNullException.ThrowIfNull(singleCommands);
        ArgumentNullException.ThrowIfNull(cascadeCommands);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        if (firmwareFileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            firmwareFileName is "." or ".." ||
            Path.GetFileName(firmwareFileName) != firmwareFileName)
        {
            throw new ArgumentException("Firmware file name must be a plain file name.", nameof(firmwareFileName));
        }

        _singleCommands = [.. singleCommands];
        _cascadeCommands = [.. cascadeCommands];
        _cascadeExtendedCommands = cascadeExtendedCommands is null ? null : [.. cascadeExtendedCommands];
        _twoChipCommands = twoChipCommands is null ? null : [.. twoChipCommands];
        _threeChipCommands = threeChipCommands is null ? null : [.. threeChipCommands];
        _branchRules = BuildBranchRules(branchRules);
        if (_singleCommands.Length == 0 || _cascadeCommands.Length == 0)
        {
            throw new ArgumentException("Postbuild profile must declare both single and cascade command branches.");
        }

        if (_cascadeExtendedCommands is { Length: 0 } ||
            _twoChipCommands is { Length: 0 } ||
            _threeChipCommands is { Length: 0 })
        {
            throw new ArgumentException("Explicit IC-count command branches cannot be empty.");
        }

        ProcessorId = processorId;
        IcId = icId;
        ToolBindingId = toolBindingId;
        FirmwareFileName = firmwareFileName;
        Evidence = evidence;
        DisplayCategory = CreateDisplayCategory(evidence, processorId);
        AssemblyKind = assemblyKind;
        CommonFwVersionRule = commonFwVersionRule;
    }

    /// <summary>Processor id referenced by composition profiles.</summary>
    public string ProcessorId { get; }

    /// <summary>IC id covered by this postbuild profile.</summary>
    public string IcId { get; }

    /// <summary>Approved external tool binding id.</summary>
    public string ToolBindingId { get; }

    /// <summary>Firmware file name used inside the staging output directory.</summary>
    public string FirmwareFileName { get; }

    /// <summary>Single-chip command branch.</summary>
    public IReadOnlyList<LegacyCombinerPostbuildCommand> SingleCommands => _singleCommands;

    /// <summary>Cascade command branch.</summary>
    public IReadOnlyList<LegacyCombinerPostbuildCommand> CascadeCommands => _cascadeCommands;

    /// <summary>Optional extended cascade command branch.</summary>
    public IReadOnlyList<LegacyCombinerPostbuildCommand>? CascadeExtendedCommands => _cascadeExtendedCommands;

    /// <summary>Optional explicit two-chip command branch.</summary>
    public IReadOnlyList<LegacyCombinerPostbuildCommand>? TwoChipCommands => _twoChipCommands;

    /// <summary>Optional explicit three-chip command branch.</summary>
    public IReadOnlyList<LegacyCombinerPostbuildCommand>? ThreeChipCommands => _threeChipCommands;

    /// <summary>Profile-specific IC number tokens accepted by the source postbuild script.</summary>
    public IReadOnlyDictionary<string, LegacyCombinerPostbuildBranch> BranchRules => _branchRules;

    /// <summary>Reference files that justify this command profile.</summary>
    public string Evidence { get; }

    /// <summary>Short display category derived from the primary postbuild evidence source.</summary>
    public string DisplayCategory { get; }

    /// <summary>Declares whether postbuild output is final flash or refreshed TP_FW requiring assembly.</summary>
    public LegacyCombinerPostbuildAssemblyKind AssemblyKind { get; }

    /// <summary>Optional Common FW category rule for ICs with versioned postbuild references.</summary>
    public LegacyCombinerCommonFwVersionRule? CommonFwVersionRule { get; }

    private static Dictionary<string, LegacyCombinerPostbuildBranch> BuildBranchRules(
        IEnumerable<LegacyCombinerPostbuildBranchRule>? branchRules)
    {
        LegacyCombinerPostbuildBranchRule[] rules = branchRules is null
            ? [
                new LegacyCombinerPostbuildBranchRule(IcNumberSelectionTokens.SingleChip, LegacyCombinerPostbuildBranch.SingleChip),
                new LegacyCombinerPostbuildBranchRule("1", LegacyCombinerPostbuildBranch.SingleChip),
                new LegacyCombinerPostbuildBranchRule(IcNumberSelectionTokens.Cascade, LegacyCombinerPostbuildBranch.Cascade),
            ]
            : [.. branchRules];

        Dictionary<string, LegacyCombinerPostbuildBranch> byToken = new(StringComparer.Ordinal);
        foreach (LegacyCombinerPostbuildBranchRule rule in rules)
        {
            if (!byToken.TryAdd(rule.Token, rule.Branch))
            {
                throw new ArgumentException(
                    $"Postbuild branch token '{rule.Token}' is declared more than once.",
                    nameof(branchRules));
            }
        }

        return byToken;
    }

    private static string CreateDisplayCategory(string evidence, string processorId)
    {
        string primaryEvidence = evidence.Split(';', StringSplitOptions.TrimEntries)[0];
        string fileName = Path.GetFileName(primaryEvidence.Replace('\\', '/'));
        string category = string.IsNullOrWhiteSpace(fileName)
            ? processorId
            : Path.GetFileNameWithoutExtension(fileName);
        const string prefix = "PostbuildSetup_";
        return category.StartsWith(prefix, StringComparison.Ordinal)
            ? category[prefix.Length..]
            : category;
    }
}
