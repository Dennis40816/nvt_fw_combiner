using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests built-in synthetic standard merge profile evidence.</summary>
public sealed class BuiltInStandardMergeProfilesTests
{
    /// <summary>Verifies the synthetic standard merge profile compiles into two ordered copy operations.</summary>
    [Fact]
    public void SyntheticStandardMergeCompilesToDpThenTpCopyPlan()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.SyntheticStandardMerge;

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess);
        Assert.Equal("synthetic-standard-merge", profile.ProfileId);
        Assert.Equal("NT-SYNTHETIC", profile.IcId);
        Assert.Equal("standard-merge", profile.ModeId);
        Assert.Equal("standard-merge", profile.ExperienceId);
        Assert.Equal(CompositionKind.Merge, profile.CompositionKind);
        Assert.Equal(["copy-dp", "copy-tp"], result.Plan!.OrderedOperations.Select(operation => operation.OperationId));
        Assert.All(result.Plan.OrderedOperations, operation => Assert.Equal(OverlapPolicy.Reject, operation.OverlapPolicy));
    }

    /// <summary>Verifies every gen_flash standard merge profile compiles with fixed profile operations.</summary>
    [Fact]
    public void GenFlashStandardMergeProfilesCompile()
    {
        string[] expectedIcIds =
        [
            "NT51920",
            "NT51923",
            "NT51926",
            "NT51927",
            "NT51928",
            "NT51929",
            "NT51931",
            "NT51932",
        ];

        Assert.Equal(expectedIcIds, BuiltInStandardMergeProfiles.GenFlashStandardMergeProfiles.Select(profile => profile.IcId));
        foreach (CompositionProfileDefinition profile in BuiltInStandardMergeProfiles.GenFlashStandardMergeProfiles)
        {
            ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

            Assert.True(result.IsSuccess);
            Assert.Equal("standard-merge", profile.ModeId);
            Assert.Equal("standard-merge", profile.ExperienceId);
            Assert.Equal(CompositionKind.Merge, profile.CompositionKind);
            Assert.Equal(ImageInitializationKind.Blank, profile.Initialization.Kind);
            Assert.All(result.Plan!.OrderedOperations, operation => Assert.Equal(OverlapPolicy.Reject, operation.OverlapPolicy));
        }
    }

    /// <summary>Verifies owner-confirmed Standard Merge aliases copy the same ranges as their reference ICs.</summary>
    [Theory]
    [InlineData("NT51917", "NT51927")]
    [InlineData("NT51919", "NT51929")]
    public void OwnerConfirmedAliasStandardMergeProfilesFollowReferenceRanges(string aliasIcId, string referenceIcId)
    {
        CompositionProfileDefinition alias = BuiltInStandardMergeProfiles.OwnerConfirmedAliasStandardMergeProfiles
            .Single(profile => profile.IcId == aliasIcId);
        CompositionProfileDefinition reference = BuiltInStandardMergeProfiles.GenFlashStandardMergeProfiles
            .Single(profile => profile.IcId == referenceIcId);

        ProfileCompileResult aliasCompile = CompositionProfileCompiler.Compile(alias, []);
        ProfileCompileResult referenceCompile = CompositionProfileCompiler.Compile(reference, []);

        Assert.True(aliasCompile.IsSuccess, FormatIssues(aliasCompile.Issues));
        Assert.True(referenceCompile.IsSuccess, FormatIssues(referenceCompile.Issues));
        Assert.EndsWith("-gen-flash-alias", alias.ProfileId, StringComparison.Ordinal);
        Assert.Equal(reference.Initialization.Capacity, alias.Initialization.Capacity);
        Assert.Equal(
            reference.AddressSpaces
                .Where(space => space.Mutability == AddressSpaceMutability.Immutable)
                .Select(space => (space.AddressSpaceId, space.Length)),
            alias.AddressSpaces
                .Where(space => space.Mutability == AddressSpaceMutability.Immutable)
                .Select(space => (space.AddressSpaceId, space.Length)));
        Assert.Equal(
            referenceCompile.Plan!.OrderedOperations.Select(operation => (
                operation.OperationId,
                operation.Sequence,
                operation.SourceSpaceId,
                operation.SourceRange,
                operation.TargetRange)),
            aliasCompile.Plan!.OrderedOperations.Select(operation => (
                operation.OperationId,
                operation.Sequence,
                operation.SourceSpaceId,
                operation.SourceRange,
                operation.TargetRange)));
    }

    /// <summary>Verifies the executable Standard Merge catalog exposes only profiles with current release evidence gates.</summary>
    [Fact]
    public void ExecutableStandardMergeProfilesIncludeOwnerConfirmedAliases()
    {
        string[] expectedIcIds =
        [
            "NT51917",
            "NT51919",
            "NT51920",
            "NT51923",
            "NT51926",
            "NT51927",
            "NT51928",
            "NT51929",
            "NT51930",
            "NT51931",
            "NT51932",
        ];

        Assert.Equal(
            expectedIcIds,
            BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles
                .Select(profile => profile.IcId)
                .Order(StringComparer.Ordinal));
        Assert.DoesNotContain(
            BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles,
            profile => profile.IcId == "NT-SYNTHETIC");
        Assert.Contains(
            BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles,
            profile => profile.IcId == "NT51917" && profile.ProfileId == "nt51917-standard-merge-gen-flash-alias");
        Assert.Contains(
            BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles,
            profile => profile.IcId == "NT51919" && profile.ProfileId == "nt51919-standard-merge-gen-flash-alias");
        Assert.DoesNotContain(
            BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles,
            profile => profile.IcId is "NT51950" or "NT51951");
    }

    /// <summary>Verifies NT51930 Standard Merge uses the flash-map dynamic layout ranges.</summary>
    [Fact]
    public void FlashMapNt51930UsesDynamicDpAndTpRanges()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.FlashMapStandardMergeProfiles
            .Single(item => item.IcId == "NT51930");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Equal("nt51930-standard-merge-flashmap", profile.ProfileId);
        Assert.Equal(0x40000, profile.Initialization.Capacity);
        Assert.Equal(
            ["copy-tp", "copy-dp"],
            result.Plan!.OrderedOperations.Select(operation => operation.OperationId));
        Assert.Contains(result.Plan.OrderedOperations, operation =>
            operation.OperationId == "copy-tp" &&
            operation.SourceRange == ByteRange.FromStartEndExclusive(0x07000, 0x40000) &&
            operation.TargetRange == ByteRange.FromStartEndExclusive(0x07000, 0x40000));
        Assert.Contains(result.Plan.OrderedOperations, operation =>
            operation.OperationId == "copy-dp" &&
            operation.SourceRange == ByteRange.FromStartEndExclusive(0x00000, 0x06000) &&
            operation.TargetRange == ByteRange.FromStartEndExclusive(0x00000, 0x06000));
    }

    /// <summary>Verifies NT51950/NT51951 Standard Merge copies DP first, then overlays the TP range.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void DpPerspectiveProfilesCopyDpThenOverlayTp(string icId)
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.DpPerspectiveStandardMergeProfiles
            .Single(item => item.IcId == icId);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Equal(0x100000, profile.Initialization.Capacity);
        AddressSpace dpInput = Assert.Single(profile.AddressSpaces, space => space.AddressSpaceId == "dp-input");
        Assert.Equal(0x100000, dpInput.Length);
        Assert.Equal((byte?)0x00, dpInput.InputPaddingByte);
        Assert.Equal([0x40000, 0x80000, 0x100000], dpInput.AllowedInputLengths);
        Assert.Contains(profile.AddressSpaces, space =>
            space.AddressSpaceId == "dp-input" &&
            space.Length == 0x100000 &&
            space.InputPaddingByte == 0x00);
        Assert.Contains(profile.AddressSpaces, space =>
            space.AddressSpaceId == "tp-input" &&
            space.Length == 0x37000 &&
            space.InputPaddingByte is null);
        Assert.Equal(
            ["copy-dp-container", "overlay-tp"],
            result.Plan!.OrderedOperations.Select(operation => operation.OperationId));

        CompositionOperation dpCopy = result.Plan.OrderedOperations[0];
        CompositionOperation tpOverlay = result.Plan.OrderedOperations[1];
        Assert.Equal(new ByteRange(0, 0x100000), dpCopy.TargetRange);
        Assert.Equal(ByteRange.FromStartEndExclusive(0x0A000, 0x37000), tpOverlay.TargetRange);
        Assert.Equal(OverlapPolicy.ReplaceExisting, tpOverlay.OverlapPolicy);
    }

    /// <summary>Verifies DP Perspective Standard Merge preserves customer information from the DP input.</summary>
    [Fact]
    public void DpPerspectiveExecutionPreservesCustomerInfoFromDp()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.DpPerspectiveStandardMergeProfiles
            .Single(item => item.IcId == "NT51950");
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        Assert.True(compile.IsSuccess, FormatIssues(compile.Issues));

        byte[] dp = new byte[0x100000];
        byte[] tp = new byte[0x37000];
        Array.Fill(dp, (byte)0x11);
        Array.Fill(tp, (byte)0x22);
        Array.Fill(dp, (byte)0xCA, 0x37000, 0x1000);
        Array.Fill(tp, (byte)0xDD, 0x0A000, 0x2D000);

        CompositionExecutionResult result = CompositionEngine.Execute(
            compile.Plan!,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-input"] = dp,
                ["tp-input"] = tp,
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        byte[] output = result.OutputBytes.ToArray();
        Assert.All(output[..0x0A000], value => Assert.Equal(0x11, value));
        Assert.All(output[0x0A000..0x37000], value => Assert.Equal(0xDD, value));
        Assert.All(output[0x37000..0x38000], value => Assert.Equal(0xCA, value));
        Assert.All(output[0x38000..], value => Assert.Equal(0x11, value));
    }

    /// <summary>Verifies approved NT51950/NT51951 DP variants are padded to the 0x100000 work container.</summary>
    [Theory]
    [InlineData(0x40000)]
    [InlineData(0x80000)]
    [InlineData(0x100000)]
    public void DpPerspectiveExecutionAcceptsDeclaredDpInputLengths(int dpLength)
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.DpPerspectiveStandardMergeProfiles
            .Single(item => item.IcId == "NT51951");
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        Assert.True(compile.IsSuccess, FormatIssues(compile.Issues));

        byte[] dp = new byte[dpLength];
        byte[] tp = new byte[0x37000];
        Array.Fill(dp, (byte)0x11);
        Array.Fill(tp, (byte)0xDD);
        Array.Fill(dp, (byte)0xCA, 0x37000, 0x1000);

        CompositionExecutionResult result = CompositionEngine.Execute(
            compile.Plan!,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-input"] = dp,
                ["tp-input"] = tp,
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        byte[] output = result.OutputBytes.ToArray();
        Assert.Equal(0x100000, output.Length);
        Assert.All(output[..0x0A000], value => Assert.Equal(0x11, value));
        Assert.All(output[0x0A000..0x37000], value => Assert.Equal(0xDD, value));
        Assert.All(output[0x37000..0x38000], value => Assert.Equal(0xCA, value));
        Assert.All(output[0x38000..dpLength], value => Assert.Equal(0x11, value));
        Assert.All(output[dpLength..], value => Assert.Equal(0x00, value));
    }

    /// <summary>Verifies NT51950/NT51951 DP Perspective merge rejects DP inputs outside the three approved sizes.</summary>
    [Theory]
    [InlineData(0x3FFFF)]
    [InlineData(0x60000)]
    [InlineData(0x90000)]
    public void DpPerspectiveExecutionRejectsUnexpectedDpInputSize(int dpLength)
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.DpPerspectiveStandardMergeProfiles
            .Single(item => item.IcId == "NT51950");
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        Assert.True(compile.IsSuccess, FormatIssues(compile.Issues));

        CompositionExecutionResult result = CompositionEngine.Execute(
            compile.Plan!,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-input"] = new byte[dpLength],
                ["tp-input"] = new byte[0x37000],
            }));

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("input.address-space.length-mismatch", issue.Code);
        Assert.Contains("0x40000, 0x80000, 0x100000", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies NT51950/NT51951 DP Perspective merge rejects DP inputs larger than the max container.</summary>
    [Fact]
    public void DpPerspectiveExecutionRejectsOversizedDpInput()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.DpPerspectiveStandardMergeProfiles
            .Single(item => item.IcId == "NT51950");
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        Assert.True(compile.IsSuccess, FormatIssues(compile.Issues));

        CompositionExecutionResult result = CompositionEngine.Execute(
            compile.Plan!,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-input"] = new byte[0x100001],
                ["tp-input"] = new byte[0x37000],
            }));

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == "input.address-space.length-mismatch");
    }

    /// <summary>Verifies NT51950/NT51951 TP input must contain the declared overlay window.</summary>
    [Fact]
    public void DpPerspectiveExecutionRejectsTpInputWithoutOverlayWindow()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.DpPerspectiveStandardMergeProfiles
            .Single(item => item.IcId == "NT51950");
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        Assert.True(compile.IsSuccess, FormatIssues(compile.Issues));

        CompositionExecutionResult result = CompositionEngine.Execute(
            compile.Plan!,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-input"] = new byte[0x40000],
                ["tp-input"] = new byte[0x36FFF],
            }));

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("input.address-space.length-mismatch", issue.Code);
        Assert.Contains("declared 225280 bytes", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies NT51928 carries the extra LD copy after TP and DP, matching gen_flash order.</summary>
    [Fact]
    public void GenFlashNt51928IncludesLdCopyAfterDp()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.GenFlashStandardMergeProfiles
            .Single(item => item.IcId == "NT51928");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess);
        Assert.Equal(["copy-tp", "copy-dp", "copy-ld"], result.Plan!.OrderedOperations.Select(operation => operation.OperationId));
        Assert.Contains(profile.AddressSpaces, addressSpace => addressSpace.AddressSpaceId == "ld-input");
    }

    /// <summary>Verifies gen_flash profiles declare the actual source artifact sizes used by golden fixtures.</summary>
    [Fact]
    public void GenFlashStandardMergeProfilesDeclareGoldenSourceArtifactSizes()
    {
        Dictionary<string, Dictionary<string, long>> expectedLengths = new(StringComparer.Ordinal)
        {
            ["NT51920"] = new(StringComparer.Ordinal) { ["dp-input"] = 0x40000, ["tp-input"] = 0x30000 },
            ["NT51923"] = new(StringComparer.Ordinal) { ["dp-input"] = 0x40000, ["tp-input"] = 0x3C000 },
            ["NT51926"] = new(StringComparer.Ordinal) { ["dp-input"] = 0x40000, ["tp-input"] = 0x3C000 },
            ["NT51927"] = new(StringComparer.Ordinal) { ["dp-input"] = 0x200000, ["tp-input"] = 0x35000 },
            ["NT51928"] = new(StringComparer.Ordinal)
            {
                ["dp-input"] = 0x80000,
                ["ld-input"] = 0x80000,
                ["tp-input"] = 0x35000,
            },
            ["NT51929"] = new(StringComparer.Ordinal) { ["dp-input"] = 0x40000, ["tp-input"] = 0x40000 },
            ["NT51931"] = new(StringComparer.Ordinal) { ["dp-input"] = 0x80000, ["tp-input"] = 0x3C000 },
            ["NT51932"] = new(StringComparer.Ordinal) { ["dp-input"] = 0x40000, ["tp-input"] = 0x40000 },
        };

        foreach (CompositionProfileDefinition profile in BuiltInStandardMergeProfiles.GenFlashStandardMergeProfiles)
        {
            var actualLengths = profile.AddressSpaces
                .Where(addressSpace => addressSpace.Mutability == AddressSpaceMutability.Immutable)
                .ToDictionary(
                    addressSpace => addressSpace.AddressSpaceId,
                    addressSpace => addressSpace.Length,
                    StringComparer.Ordinal);

            Assert.Equal(expectedLengths[profile.IcId], actualLengths);
        }
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }
}
