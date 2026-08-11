using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

internal static class BuiltInPostbuildProfileCatalog
{
    private const string RelativePath = "profiles/built-in/ctrlram-postbuild-v2/catalog.json";
    private const string ExpectedSha256 = "417adb68d222dfe3bd02e9fbaf274b90f68e3fc99a01c6679d3e900a710313fc";
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
        if (document.SchemaVersion != "2.3" ||
            document.DiffDlmPolicies is null ||
            document.Profiles is null)
        {
            throw new InvalidDataException(
                "Built-in CtrlRAM Postbuild catalog must use schema 2.3 with DiffDLM policies and profiles.");
        }

        var diffDlmPolicies =
            document.DiffDlmPolicies
                .Select(CreateDiffDlmPolicy)
                .ToDictionary(static policy => policy.PolicyId, StringComparer.Ordinal);
        if (diffDlmPolicies.Count != document.DiffDlmPolicies.Count)
        {
            throw new InvalidDataException("Built-in CtrlRAM Postbuild catalog repeats a DiffDLM policy id.");
        }

        ProfileDocument[] sources = [.. document.Profiles];
        LegacyCombinerPostbuildProfile[] declaredProfiles =
        [
            .. sources.Select(source => CreateProfile(source, diffDlmPolicies)),
        ];
        if (declaredProfiles.Select(static profile => profile.ProcessorId).Distinct(StringComparer.Ordinal).Count() != declaredProfiles.Length)
        {
            throw new InvalidDataException("Built-in CtrlRAM Postbuild catalog repeats a processor id.");
        }

        LegacyCombinerPostbuildProfile[] runtimeProfiles =
        [
            .. declaredProfiles.Where((_, index) => IsRuntimeProfile(sources[index])),
        ];
        ValidateRuntimeIntervals(runtimeProfiles);
        return Array.AsReadOnly(runtimeProfiles);
    }

    internal static IReadOnlyList<LegacyCombinerPostbuildProfile> GetProfiles(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return [
            .. All
                .Where(profile => StringComparer.Ordinal.Equals(profile.IcId, icId))
                .OrderBy(static profile => profile.EffectiveCommonFwVersion)
                .ThenBy(static profile => profile.ProcessorId, StringComparer.Ordinal),
        ];
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

        bool hasVersion = LegacyCombinerCommonFwVersion.TryParse(
            commonFwVersion,
            out LegacyCombinerCommonFwVersion version);
        if (hasVersion && version.CompareTo(LegacyCombinerCommonFwVersion.MinimumSupported) < 0)
        {
            postbuildProfile = null;
            issue = $"{icId} Common FW {version} is below the minimum supported version " +
                $"{LegacyCombinerCommonFwVersion.MinimumSupported}.";
            return false;
        }

        if (profiles.Count == 1)
        {
            postbuildProfile = profiles[0];
            issue = null;
            return true;
        }

        if (!hasVersion)
        {
            postbuildProfile = null;
            issue = $"{icId} has multiple runtime postbuild profiles; a valid three-component " +
                $"base FWConfig Common FW version is required. Intervals: {DescribeIntervals(profiles)}.";
            return false;
        }

        postbuildProfile = profiles.Last(profile =>
            profile.EffectiveCommonFwVersion.CompareTo(version) <= 0);
        issue = null;
        return true;
    }

    private static string DescribeIntervals(IReadOnlyList<LegacyCombinerPostbuildProfile> profiles)
    {
        string[] descriptions = new string[profiles.Count];
        for (int index = 0; index < profiles.Count; index++)
        {
            LegacyCombinerPostbuildProfile profile = profiles[index];
            string end = index + 1 < profiles.Count
                ? profiles[index + 1].EffectiveCommonFwVersion.ToString()
                : "infinity";
            descriptions[index] =
                $"[{profile.EffectiveCommonFwVersion}, {end}) => {profile.DisplayCategory}";
        }

        return string.Join("; ", descriptions);
    }

    private static LegacyCombinerPostbuildProfile CreateProfile(
        ProfileDocument source,
        IReadOnlyDictionary<string, LegacyCombinerDiffDlmPolicy> diffDlmPolicies)
    {
        try
        {
            return CreateValidatedProfile(source, diffDlmPolicies);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                $"Built-in CtrlRAM Postbuild catalog has an invalid profile '{source.ProcessorId}'.",
                exception);
        }
    }

    private static LegacyCombinerPostbuildProfile CreateValidatedProfile(
        ProfileDocument source,
        IReadOnlyDictionary<string, LegacyCombinerDiffDlmPolicy> diffDlmPolicies)
    {
        LegacyCombinerDiffDlmPolicy? diffDlmPolicy = source.DiffDlmPolicyId is null
            ? null
            : diffDlmPolicies.TryGetValue(
                source.DiffDlmPolicyId,
                out LegacyCombinerDiffDlmPolicy? resolved)
                ? resolved
                : throw Invalid("profile diffDlmPolicyId");

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
            Required(source.PlanSelectors, "planSelectors").Select(CreatePlanSelector),
            source.AssemblyKind switch
            {
                "in-place-firmware-image" => LegacyCombinerPostbuildAssemblyKind.InPlaceFirmwareImage,
                "refreshed-tp-then-standard-merge" => LegacyCombinerPostbuildAssemblyKind.RefreshedTpThenStandardMerge,
                _ => throw Invalid("assemblyKind"),
            },
            ParseEffectiveCommonFwVersion(source.EffectiveCommonFwVersion),
            source.FirmwareConfigWriteRoute switch
            {
                "command-source-to-canonical-backup" =>
                    LegacyCombinerFirmwareConfigWriteRoute.CommandSourceToCanonicalBackup,
                "primary-to-canonical-backup" =>
                    LegacyCombinerFirmwareConfigWriteRoute.PrimaryToCanonicalBackup,
                "unavailable" => LegacyCombinerFirmwareConfigWriteRoute.Unavailable,
                _ => throw Invalid("firmwareConfigWriteRoute"),
            },
            diffDlmPolicy);
    }

    private static LegacyCombinerDiffDlmPolicy CreateDiffDlmPolicy(
        DiffDlmPolicyDocument source)
    {
        try
        {
            return new LegacyCombinerDiffDlmPolicy(
                source.PolicyId,
                source.SourceFileName,
                source.StagedArtifactId,
                source.SourceRecordStride,
                source.TargetBase,
                source.TargetRecordStride,
                CreateRange(source.WritableRange, "diffDlmPolicies.writableRange"),
                Required(source.PreservationMasks, "diffDlmPolicies.preservationMasks")
                    .Select(mask => new LegacyCombinerDiffDlmMask(
                        mask.Kind switch
                        {
                            "keep-reference" => LegacyCombinerDiffDlmMaskKind.KeepReference,
                            _ => throw Invalid("diffDlmPolicies.preservationMasks.kind"),
                        },
                        CreateRange(mask.Range, "diffDlmPolicies.preservationMasks.range"))),
                source.MinimumIcCount,
                source.MaximumIcCount,
                source.ActiveRecordCountOffset,
                source.IndependentNfSourceFileName,
                source.FirmwareConfigBackupAlignment,
                source.FirmwareConfigBackupLength,
                CreateRange(
                    source.FirmwareConfigBackupAuthority,
                    "diffDlmPolicies.firmwareConfigBackupAuthority"),
                Required(source.EvidenceRefs, "diffDlmPolicies.evidenceRefs"),
                source.FixedFirmwareConfigBackupStart);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                $"Built-in CtrlRAM Postbuild catalog has an invalid DiffDLM policy '{source.PolicyId}'.",
                exception);
        }
    }

    private static ByteRange CreateRange(RangeDocument source, string name)
    {
        return source is null
            ? throw Invalid(name)
            : new ByteRange(source.Start, source.Length);
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

    private static LegacyCombinerCommonFwVersion ParseEffectiveCommonFwVersion(string? value)
    {
        return LegacyCombinerCommonFwVersion.TryParse(value, out LegacyCombinerCommonFwVersion version)
            ? version
            : throw Invalid("effectiveCommonFwVersion");
    }

    private static LegacyCombinerPostbuildPlanSelector CreatePlanSelector(PlanSelectorDocument source)
    {
        return new LegacyCombinerPostbuildPlanSelector(
            source.Kind switch
            {
                "single" => LegacyCombinerPostbuildPlanSelectorKind.SingleChip,
                "generic-cascade" => LegacyCombinerPostbuildPlanSelectorKind.GenericCascade,
                "exact-count" => LegacyCombinerPostbuildPlanSelectorKind.ExactCount,
                "count-range" => LegacyCombinerPostbuildPlanSelectorKind.CountRange,
                _ => throw Invalid("planSelectors.kind"),
            },
            source.Branch switch
            {
                "single" => LegacyCombinerPostbuildBranch.SingleChip,
                "cascade" => LegacyCombinerPostbuildBranch.Cascade,
                "two-chip" => LegacyCombinerPostbuildBranch.TwoChip,
                "three-chip" => LegacyCombinerPostbuildBranch.ThreeChip,
                _ => throw Invalid("planSelectors.branch"),
            },
            source.Count,
            source.MinimumCount,
            source.MaximumCount);
    }

    private static void ValidateRuntimeIntervals(IReadOnlyList<LegacyCombinerPostbuildProfile> profiles)
    {
        foreach (IGrouping<string, LegacyCombinerPostbuildProfile> group in
                 profiles.GroupBy(static profile => profile.IcId, StringComparer.Ordinal))
        {
            LegacyCombinerPostbuildProfile[] ordered =
            [
                .. group
                    .OrderBy(static profile => profile.EffectiveCommonFwVersion)
                    .ThenBy(static profile => profile.ProcessorId, StringComparer.Ordinal),
            ];
            if (ordered[0].EffectiveCommonFwVersion != LegacyCombinerCommonFwVersion.MinimumSupported)
            {
                throw Invalid($"{group.Key} first runtime effectiveCommonFwVersion");
            }

            for (int index = 1; index < ordered.Length; index++)
            {
                if (ordered[index - 1].EffectiveCommonFwVersion == ordered[index].EffectiveCommonFwVersion)
                {
                    throw Invalid($"{group.Key} duplicate runtime effectiveCommonFwVersion");
                }
            }
        }
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
            source.StagedArtifactId,
            string.IsNullOrWhiteSpace(source.SectionId)
                ? throw Invalid("block.sectionId")
                : source.SectionId);
    }

    private static void ValidateArguments(LegacyCombinerCommandFamily family, string mode, string? crc)
    {
        bool approved = family switch
        {
            LegacyCombinerCommandFamily.NormalMode => mode == "CRC_Enable" && crc is null,
            LegacyCombinerCommandFamily.MergeMode => mode == "MERGE_MODE" && crc is null,
            LegacyCombinerCommandFamily.CrcOnlyMode => mode == "NT51927BASED_GEN_CRC_MODE" && crc == "CRC32",
            LegacyCombinerCommandFamily.NtBasedNormalMode =>
                mode is "NT51932BASED_NORMAL_MODE" or "NT51950BASED_NORMAL_MODE" && crc == "CRC8",
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

    private sealed record CatalogDocument(
        string SchemaVersion,
        IReadOnlyList<DiffDlmPolicyDocument>? DiffDlmPolicies,
        IReadOnlyList<ProfileDocument>? Profiles);

    private sealed record ProfileDocument(
        string ProcessorId,
        string IcId,
        string ToolBindingId,
        string FirmwareFileName,
        IReadOnlyList<CommandDocument>? SingleCommands,
        IReadOnlyList<CommandDocument>? CascadeCommands,
        IReadOnlyList<CommandDocument>? TwoChipCommands,
        IReadOnlyList<CommandDocument>? ThreeChipCommands,
        IReadOnlyList<PlanSelectorDocument>? PlanSelectors,
        string AssemblyKind,
        string? EffectiveCommonFwVersion,
        string FirmwareConfigWriteRoute,
        string? Availability,
        string Evidence,
        string? DiffDlmPolicyId);

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
        string? StagedArtifactId,
        string SectionId);

    private sealed record DiffDlmPolicyDocument(
        string PolicyId,
        string SourceFileName,
        string StagedArtifactId,
        long SourceRecordStride,
        long TargetBase,
        long TargetRecordStride,
        RangeDocument WritableRange,
        IReadOnlyList<DiffDlmMaskDocument>? PreservationMasks,
        int MinimumIcCount,
        int MaximumIcCount,
        int ActiveRecordCountOffset,
        string IndependentNfSourceFileName,
        int FirmwareConfigBackupAlignment,
        long FirmwareConfigBackupLength,
        RangeDocument FirmwareConfigBackupAuthority,
        IReadOnlyList<string>? EvidenceRefs,
        long? FixedFirmwareConfigBackupStart);

    private sealed record DiffDlmMaskDocument(string Kind, RangeDocument Range);

    private sealed record RangeDocument(long Start, long Length);

    private sealed record PlanSelectorDocument(
        string Kind,
        string Branch,
        int? Count,
        int? MinimumCount,
        int? MaximumCount);

}
