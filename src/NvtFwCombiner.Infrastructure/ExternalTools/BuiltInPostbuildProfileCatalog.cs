using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

internal static class BuiltInPostbuildProfileCatalog
{
    private const string RelativePath = "profiles/built-in/ctrlram-postbuild-v2/catalog.json";
    private const string ExpectedSha256 = "08c73483acab41e9c87d38064dd52186668fe9f89606be145c161f34aa171e65";
    private static readonly Lazy<IReadOnlyList<LegacyCombinerPostbuildProfile>> Profiles = new(Load);

    internal static IReadOnlyList<LegacyCombinerPostbuildProfile> All => Profiles.Value;

    private static IReadOnlyList<LegacyCombinerPostbuildProfile> Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, RelativePath.Replace('/', Path.DirectorySeparatorChar));
        byte[] bytes = File.ReadAllBytes(path);
        return Load(bytes, ExpectedSha256);
    }

    internal static IReadOnlyList<LegacyCombinerPostbuildProfile> Load(
        ReadOnlySpan<byte> bytes,
        string expectedSha256)
    {
        CatalogDocument document = PinnedJsonCatalogLoader.Load<CatalogDocument>(
            bytes,
            expectedSha256,
            "Built-in CtrlRAM Postbuild catalog",
            "Built-in CtrlRAM Postbuild catalog is empty.");
        if (document.SchemaVersion != "2.0" || document.Profiles is null)
        {
            throw new InvalidDataException("Built-in CtrlRAM Postbuild catalog must use schema 2.0 with profiles.");
        }

        ProfileDocument[] sources = [.. document.Profiles];
        LegacyCombinerPostbuildProfile[] declaredProfiles = [.. sources.Select(CreateProfile)];
        if (declaredProfiles.Select(static profile => profile.ProcessorId).Distinct(StringComparer.Ordinal).Count() != declaredProfiles.Length)
        {
            throw new InvalidDataException("Built-in CtrlRAM Postbuild catalog repeats a processor id.");
        }

        LegacyCombinerPostbuildProfile[] runtimeProfiles =
        [
            .. declaredProfiles.Where((_, index) => IsRuntimeProfile(sources[index])),
        ];
        return Array.AsReadOnly(runtimeProfiles);
    }

    internal static IReadOnlyList<LegacyCombinerPostbuildProfile> GetProfiles(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return [.. All.Where(profile => StringComparer.Ordinal.Equals(profile.IcId, icId))];
    }

    internal static bool TryGetDefaultProfile(
        string icId,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = GetProfiles(icId);
        postbuildProfile = profiles.Count == 0 ? null : profiles[0];
        return postbuildProfile is not null;
    }

    internal static bool TrySelectProfileForCommonFwVersion(
        string icId,
        string? commonFwVersion,
        out LegacyCombinerPostbuildProfile? postbuildProfile,
        out string? issue)
    {
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = GetProfiles(icId);
        if (profiles.Count == 0)
        {
            postbuildProfile = null;
            issue = $"No legacy Combiner postbuild profile is registered for {icId}.";
            return false;
        }

        if (profiles.Count == 1 && profiles[0].CommonFwVersionRule is null)
        {
            postbuildProfile = profiles[0];
            issue = null;
            return true;
        }

        LegacyCombinerPostbuildProfile[] matches = string.IsNullOrWhiteSpace(commonFwVersion)
            ? []
            : [.. profiles.Where(profile => profile.CommonFwVersionRule?.Matches(commonFwVersion) == true)];
        if (matches.Length == 1)
        {
            postbuildProfile = matches[0];
            issue = null;
            return true;
        }

        postbuildProfile = null;
        issue = string.IsNullOrWhiteSpace(commonFwVersion)
            ? $"{icId} has a versioned postbuild category; base FWConfig Common FW version is required."
            : $"{icId} Common FW {commonFwVersion} has no approved postbuild category. Supported categories: {Describe(profiles)}.";
        return false;
    }

    private static string Describe(IEnumerable<LegacyCombinerPostbuildProfile> profiles)
    {
        string[] descriptions =
        [
            .. profiles
                .Select(static profile => profile.CommonFwVersionRule?.Description)
                .Where(static description => !string.IsNullOrWhiteSpace(description))
                .Cast<string>(),
        ];
        return descriptions.Length == 0
            ? "no versioned postbuild categories declared"
            : string.Join("; ", descriptions);
    }

    private static LegacyCombinerPostbuildProfile CreateProfile(ProfileDocument source)
    {
        return new LegacyCombinerPostbuildProfile(
            source.ProcessorId,
            source.IcId,
            source.ToolBindingId,
            source.FirmwareFileName,
            Required(source.SingleCommands, "singleCommands").Select(CreateCommand),
            Required(source.CascadeCommands, "cascadeCommands").Select(CreateCommand),
            source.Evidence,
            source.TwoChipCommands?.Select(CreateCommand),
            source.ThreeChipCommands?.Select(CreateCommand),
            Required(source.BranchRules, "branchRules").Select(rule => new LegacyCombinerPostbuildBranchRule(
                rule.Token,
                rule.Branch switch
                {
                    "single" => LegacyCombinerPostbuildBranch.SingleChip,
                    "cascade" => LegacyCombinerPostbuildBranch.Cascade,
                    "two-chip" => LegacyCombinerPostbuildBranch.TwoChip,
                    "three-chip" => LegacyCombinerPostbuildBranch.ThreeChip,
                    _ => throw Invalid("branch"),
                })),
            source.AssemblyKind switch
            {
                "in-place-firmware-image" => LegacyCombinerPostbuildAssemblyKind.InPlaceFirmwareImage,
                "refreshed-tp-then-standard-merge" => LegacyCombinerPostbuildAssemblyKind.RefreshedTpThenStandardMerge,
                _ => throw Invalid("assemblyKind"),
            },
            source.CommonFwVersionRule is null
                ? null
                : new LegacyCombinerCommonFwVersionRule(
                    source.CommonFwVersionRule.MatchKind switch
                    {
                        "exact" => LegacyCombinerCommonFwVersionMatchKind.Exact,
                        "major" => LegacyCombinerCommonFwVersionMatchKind.Major,
                        _ => throw Invalid("commonFwVersionRule.matchKind"),
                    },
                    source.CommonFwVersionRule.Pattern,
                    source.CommonFwVersionRule.Description));
    }

    private static bool IsRuntimeProfile(ProfileDocument source)
    {
        return source.Availability switch
        {
            null or "runtime" => true,
            "evidence-only" => false,
            _ => throw Invalid("profile availability"),
        };
    }

    private static LegacyCombinerPostbuildCommand CreateCommand(CommandDocument source)
    {
        LegacyCombinerCommandFamily family = source.Family switch
        {
            "normal-mode" => LegacyCombinerCommandFamily.NormalMode,
            "merge-mode" => LegacyCombinerCommandFamily.MergeMode,
            "ntbased-normal-mode" => LegacyCombinerCommandFamily.NtBasedNormalMode,
            "crc-only-mode" => LegacyCombinerCommandFamily.CrcOnlyMode,
            _ => throw Invalid("command.family"),
        };
        ValidateArguments(family, source.ModeArgument, source.CrcArgument);
        return new LegacyCombinerPostbuildCommand(
            source.CommandId,
            family,
            source.ModeArgument,
            source.CrcArgument,
            Required(source.Blocks, "command.blocks").Select(CreateBlock));
    }

    private static LegacyCombinerBlockArgument CreateBlock(BlockDocument source)
    {
        return new LegacyCombinerBlockArgument(
            source.BlockId,
            source.SourceKind switch
            {
                "firmware-image" => LegacyCombinerBlockSourceKind.FirmwareImage,
                "staged-file" => LegacyCombinerBlockSourceKind.StagedFile,
                "staged-artifact" => LegacyCombinerBlockSourceKind.StagedArtifact,
                _ => throw Invalid("block.sourceKind"),
            },
            source.SourceFileName,
            source.SourceOffset,
            new ByteRange(source.TargetStart, source.TargetLength),
            source.StagedArtifactId);
    }

    private static void ValidateArguments(LegacyCombinerCommandFamily family, string mode, string? crc)
    {
        bool approved = family switch
        {
            LegacyCombinerCommandFamily.NormalMode => mode == "CRC_Enable" && crc is null,
            LegacyCombinerCommandFamily.MergeMode => mode == "MERGE_MODE" && crc is null,
            LegacyCombinerCommandFamily.CrcOnlyMode => mode == "NT51927BASED_GEN_CRC_MODE" && crc == "CRC32",
            LegacyCombinerCommandFamily.NtBasedNormalMode =>
                mode is "NT51930BASED_NORMAL_MODE" or "NT51932BASED_NORMAL_MODE" or "NT51950BASED_NORMAL_MODE" && crc == "CRC8",
            _ => false,
        };
        if (!approved)
        {
            throw Invalid("command mode/CRC allowlist");
        }
    }

    private static IReadOnlyList<T> Required<T>(IReadOnlyList<T>? values, string name)
    {
        return values ?? throw Invalid(name);
    }

    private static InvalidDataException Invalid(string name)
    {
        return new InvalidDataException($"Built-in CtrlRAM Postbuild catalog has invalid {name}.");
    }

    private sealed record CatalogDocument(string SchemaVersion, IReadOnlyList<ProfileDocument>? Profiles);

    private sealed record ProfileDocument(
        string ProcessorId,
        string IcId,
        string ToolBindingId,
        string FirmwareFileName,
        IReadOnlyList<CommandDocument>? SingleCommands,
        IReadOnlyList<CommandDocument>? CascadeCommands,
        IReadOnlyList<CommandDocument>? TwoChipCommands,
        IReadOnlyList<CommandDocument>? ThreeChipCommands,
        IReadOnlyList<BranchRuleDocument>? BranchRules,
        string AssemblyKind,
        CommonFwVersionRuleDocument? CommonFwVersionRule,
        string? Availability,
        string Evidence);

    private sealed record CommandDocument(
        string CommandId,
        string Family,
        string ModeArgument,
        string? CrcArgument,
        IReadOnlyList<BlockDocument>? Blocks);

    private sealed record BlockDocument(
        string BlockId,
        string SourceKind,
        string SourceFileName,
        long SourceOffset,
        long TargetStart,
        long TargetLength,
        string? StagedArtifactId);

    private sealed record BranchRuleDocument(string Token, string Branch);

    private sealed record CommonFwVersionRuleDocument(string MatchKind, string Pattern, string Description);
}
