using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Legacy combiner command families observed in owner-provided postbuild scripts.</summary>
public enum LegacyCombinerCommandFamily
{
    /// <summary>Earliest normal mode, represented by CRC_Enable, CRC32_Enable, or CRC_Disable.</summary>
    NormalMode,

    /// <summary>Merge-only command shape used by NT51927 postbuild before CRC generation.</summary>
    MergeMode,

    /// <summary>Newer NTxxxxxBASED_NORMAL_MODE command shape with an explicit CRC method argument.</summary>
    NtBasedNormalMode,

    /// <summary>CRC-only command shape used after staged merge commands.</summary>
    CrcOnlyMode,
}

/// <summary>Postbuild branch selected from IC number context.</summary>
public enum LegacyCombinerPostbuildBranch
{
    /// <summary>Single-chip postbuild branch.</summary>
    SingleChip,

    /// <summary>Cascade or multi-chip postbuild branch.</summary>
    Cascade,

    /// <summary>Extended cascade postbuild branch for scripts with a larger cascade-count section.</summary>
    CascadeExtended,

    /// <summary>Explicit two-chip postbuild branch.</summary>
    TwoChip,

    /// <summary>Explicit three-chip postbuild branch.</summary>
    ThreeChip,
}

/// <summary>How a transformed postbuild firmware image becomes the final Replace output.</summary>
public enum LegacyCombinerPostbuildAssemblyKind
{
    /// <summary>The staged firmware image already uses final flash coordinates.</summary>
    InPlaceFirmwareImage,

    /// <summary>The staged firmware image is refreshed TP_FW and must be assembled back with base DP bytes.</summary>
    RefreshedTpThenStandardMerge,
}

/// <summary>How a postbuild profile matches FWConfig Common FW versions.</summary>
public enum LegacyCombinerCommonFwVersionMatchKind
{
    /// <summary>The profile applies to exactly one Common FW semantic version.</summary>
    Exact,

    /// <summary>The profile applies to a major Common FW family, such as 1.x.x.</summary>
    Major,
}

/// <summary>Common FW category rule for ICs with versioned postbuild scripts.</summary>
public sealed class LegacyCombinerCommonFwVersionRule
{
    /// <summary>Creates an exact Common FW category rule.</summary>
    public static LegacyCombinerCommonFwVersionRule Exact(
        string version,
        string postbuildSetupFileName)
    {
        return new LegacyCombinerCommonFwVersionRule(
            LegacyCombinerCommonFwVersionMatchKind.Exact,
            version,
            $"Common FW {version} => {postbuildSetupFileName}");
    }

    /// <summary>Creates a major-version Common FW category rule.</summary>
    public static LegacyCombinerCommonFwVersionRule Major(
        string majorVersion,
        string displayVersion,
        string postbuildSetupFileName)
    {
        return new LegacyCombinerCommonFwVersionRule(
            LegacyCombinerCommonFwVersionMatchKind.Major,
            majorVersion,
            $"Common FW {displayVersion} => {postbuildSetupFileName}");
    }

    /// <summary>Creates a Common FW category rule from owner-approved postbuild evidence.</summary>
    public LegacyCombinerCommonFwVersionRule(
        LegacyCombinerCommonFwVersionMatchKind matchKind,
        string pattern,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        MatchKind = matchKind;
        Pattern = pattern.Trim();
        Description = description.Trim();
    }

    /// <summary>Version matching strategy.</summary>
    public LegacyCombinerCommonFwVersionMatchKind MatchKind { get; }

    /// <summary>Exact version or major-version token used by the strategy.</summary>
    public string Pattern { get; }

    /// <summary>User-facing supported-category description.</summary>
    public string Description { get; }

    /// <summary>Returns whether the rule applies to a FWConfig Common FW version string.</summary>
    public bool Matches(string? commonFwVersion)
    {
        if (string.IsNullOrWhiteSpace(commonFwVersion))
        {
            return false;
        }

        string version = commonFwVersion.Trim();
        return MatchKind switch
        {
            LegacyCombinerCommonFwVersionMatchKind.Exact => string.Equals(version, Pattern, StringComparison.Ordinal),
            LegacyCombinerCommonFwVersionMatchKind.Major =>
                string.Equals(version, Pattern, StringComparison.Ordinal) ||
                version.StartsWith(Pattern + ".", StringComparison.Ordinal),
            _ => false,
        };
    }
}

/// <summary>One accepted IC number token for selecting a legacy postbuild branch.</summary>
public sealed class LegacyCombinerPostbuildBranchRule
{
    /// <summary>Creates a normalized branch rule from postbuild script evidence.</summary>
    public LegacyCombinerPostbuildBranchRule(string token, LegacyCombinerPostbuildBranch branch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        Token = NormalizeToken(token);
        Branch = branch;
    }

    /// <summary>Normalized user token, such as single, cascade, 1, 2, or 3.</summary>
    public string Token { get; }

    /// <summary>Branch selected by the token.</summary>
    public LegacyCombinerPostbuildBranch Branch { get; }

    internal static string NormalizeToken(string token)
    {
        string normalized = token.Trim();
        if (normalized.Length >= 2 &&
            normalized[0] == '(' &&
            normalized[^1] == ')')
        {
            normalized = normalized[1..^1].Trim();
        }

        return normalized.ToLowerInvariant();
    }
}

/// <summary>Where a combiner block argument reads its source bytes from.</summary>
public enum LegacyCombinerBlockSourceKind
{
    /// <summary>The block source is the staged firmware image itself.</summary>
    FirmwareImage,

    /// <summary>The block source is a generated file under the staged BIN directory.</summary>
    StagedFile,
}

/// <summary>One block argument passed to Combiner.exe.</summary>
public sealed class LegacyCombinerBlockArgument
{
    /// <summary>Creates a block argument from postbuild evidence.</summary>
    public LegacyCombinerBlockArgument(
        string blockId,
        LegacyCombinerBlockSourceKind sourceKind,
        string sourceFileName,
        long sourceOffset,
        ByteRange firmwareRange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        if (sourceFileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            sourceFileName is "." or ".." ||
            Path.GetFileName(sourceFileName) != sourceFileName)
        {
            throw new ArgumentException("Source file name must be a plain file name.", nameof(sourceFileName));
        }

        BlockId = blockId;
        SourceKind = sourceKind;
        SourceFileName = sourceFileName;
        SourceOffset = sourceOffset;
        FirmwareRange = firmwareRange;
    }

    /// <summary>Stable block id used in diagnostics.</summary>
    public string BlockId { get; }

    /// <summary>Source kind selected by the postbuild command.</summary>
    public LegacyCombinerBlockSourceKind SourceKind { get; }

    /// <summary>Plain source file name when <see cref="SourceKind" /> is <see cref="LegacyCombinerBlockSourceKind.StagedFile" />.</summary>
    public string SourceFileName { get; }

    /// <summary>Source offset passed to Combiner.exe.</summary>
    public long SourceOffset { get; }

    /// <summary>Destination range inside the staged firmware image.</summary>
    public ByteRange FirmwareRange { get; }
}

/// <summary>One Combiner.exe process invocation from a postbuild branch.</summary>
public sealed class LegacyCombinerPostbuildCommand
{
    private readonly LegacyCombinerBlockArgument[] _blocks;

    /// <summary>Creates a postbuild command declaration.</summary>
    public LegacyCombinerPostbuildCommand(
        string commandId,
        LegacyCombinerCommandFamily family,
        string modeArgument,
        string? crcArgument,
        IEnumerable<LegacyCombinerBlockArgument> blocks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modeArgument);
        ArgumentNullException.ThrowIfNull(blocks);

        _blocks = [.. blocks];
        if (_blocks.Length == 0 && family != LegacyCombinerCommandFamily.CrcOnlyMode)
        {
            throw new ArgumentException("Postbuild command must contain at least one block unless it is CRC-only.", nameof(blocks));
        }

        if ((family == LegacyCombinerCommandFamily.NtBasedNormalMode ||
             family == LegacyCombinerCommandFamily.CrcOnlyMode) &&
            string.IsNullOrWhiteSpace(crcArgument))
        {
            throw new ArgumentException("This command family requires a CRC argument.", nameof(crcArgument));
        }

        if ((family == LegacyCombinerCommandFamily.NormalMode ||
             family == LegacyCombinerCommandFamily.MergeMode) &&
            !string.IsNullOrWhiteSpace(crcArgument))
        {
            throw new ArgumentException("This command family encodes CRC selection in the mode argument.", nameof(crcArgument));
        }

        CommandId = commandId;
        Family = family;
        ModeArgument = modeArgument;
        CrcArgument = string.IsNullOrWhiteSpace(crcArgument) ? null : crcArgument;
    }

    /// <summary>Stable command id used in diagnostics.</summary>
    public string CommandId { get; }

    /// <summary>Command family determining argv layout.</summary>
    public LegacyCombinerCommandFamily Family { get; }

    /// <summary>First Combiner.exe argument.</summary>
    public string ModeArgument { get; }

    /// <summary>CRC method argument for NT-based normal mode commands.</summary>
    public string? CrcArgument { get; }

    /// <summary>Block arguments in postbuild order.</summary>
    public IReadOnlyList<LegacyCombinerBlockArgument> Blocks => _blocks;
}

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

    /// <summary>Declares whether postbuild output is final flash or refreshed TP_FW requiring assembly.</summary>
    public LegacyCombinerPostbuildAssemblyKind AssemblyKind { get; }

    /// <summary>Optional Common FW category rule for ICs with versioned postbuild references.</summary>
    public LegacyCombinerCommonFwVersionRule? CommonFwVersionRule { get; }

    private static Dictionary<string, LegacyCombinerPostbuildBranch> BuildBranchRules(
        IEnumerable<LegacyCombinerPostbuildBranchRule>? branchRules)
    {
        LegacyCombinerPostbuildBranchRule[] rules = branchRules is null
            ? [
                new LegacyCombinerPostbuildBranchRule("single", LegacyCombinerPostbuildBranch.SingleChip),
                new LegacyCombinerPostbuildBranchRule("1", LegacyCombinerPostbuildBranch.SingleChip),
                new LegacyCombinerPostbuildBranchRule("cascade", LegacyCombinerPostbuildBranch.Cascade),
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
}

/// <summary>Resolved postbuild command plan for one run.</summary>
public sealed class LegacyCombinerPostbuildCommandPlan
{
    private readonly LegacyCombinerPostbuildCommand[] _commands;

    /// <summary>Creates a resolved postbuild command plan.</summary>
    public LegacyCombinerPostbuildCommandPlan(
        LegacyCombinerPostbuildProfile profile,
        LegacyCombinerPostbuildBranch branch,
        IEnumerable<LegacyCombinerPostbuildCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(commands);

        _commands = [.. commands];
        if (_commands.Length == 0)
        {
            throw new ArgumentException("Resolved postbuild plan must contain at least one command.", nameof(commands));
        }

        Profile = profile;
        Branch = branch;
    }

    /// <summary>Profile selected for this run.</summary>
    public LegacyCombinerPostbuildProfile Profile { get; }

    /// <summary>Single or cascade branch selected for this run.</summary>
    public LegacyCombinerPostbuildBranch Branch { get; }

    /// <summary>Process commands in execution order.</summary>
    public IReadOnlyList<LegacyCombinerPostbuildCommand> Commands => _commands;
}
