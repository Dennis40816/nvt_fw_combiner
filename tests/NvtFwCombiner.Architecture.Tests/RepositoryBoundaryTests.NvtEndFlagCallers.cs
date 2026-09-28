using System.Text.RegularExpressions;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>
    /// NVT-END-FLAG-1113-01 (F-3) named caller inventory: the only production readers of the canonical FWConfig Backup.
    /// Each reads through a resolved end-flag declaration; a new reader needs an admitted change to this list.
    /// </summary>
    private static readonly string[] NvtBackupReaderInventory =
    [
        "src/NvtFwCombiner.Application/Composition/CompositionRunService.FinalOutputValidations.cs",
        "src/NvtFwCombiner.Application/FlashMaps/FirmwareConfigChipCountDiagnostics.cs",
        "src/NvtFwCombiner.Application/InputInspection/CompiledInputArtifactObservationService.cs",
        "src/NvtFwCombiner.Application/InputInspection/FirmwareArtifactClassificationResolver.CtrlRam.cs",
        "src/NvtFwCombiner.Infrastructure/Composition/BuiltInFirmwareInspection.Metadata.cs",
        "src/NvtFwCombiner.Infrastructure/Composition/BuiltInFirmwareInspection.cs",
    ];

    /// <summary>Only the named readers read the FWConfig Backup, and never without an end-flag declaration.</summary>
    [Fact]
    public void NvtBackupReadersAreTheNamedInventoryAndAlwaysPassADeclaration()
    {
        string sourceRoot = Path.Combine(Root.FullName, "src");
        var readers = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string path in Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                         !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            string code = string.Join('\n', File.ReadAllLines(path)
                .Where(static line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal)));
            if (!code.Contains("FirmwareConfigMetadataReader.TryReadBackup(", StringComparison.Ordinal))
            {
                continue;
            }

            string relative = Path.GetRelativePath(Root.FullName, path).Replace(Path.DirectorySeparatorChar, '/');
            _ = readers.Add(relative);
            Assert.False(UndeclaredBackupReadRegex().IsMatch(code),
                $"{relative} reads the FWConfig Backup without an NVT end-flag declaration.");
        }

        Assert.Equal(NvtBackupReaderInventory.Order(StringComparer.Ordinal), readers);
    }

    [GeneratedRegex(@"FirmwareConfigMetadataReader\.TryReadBackup\(\s*[^,()]+(?:\([^()]*\))?\s*,\s*out\b")]
    private static partial Regex UndeclaredBackupReadRegex();
}
