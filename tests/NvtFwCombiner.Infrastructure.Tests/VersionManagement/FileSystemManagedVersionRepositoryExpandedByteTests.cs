using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class FileSystemManagedVersionRepositoryTests
{
    /// <summary>Forged ZIP length hints cannot pass real verification or leave install residue.</summary>
    [Fact]
    public async Task UnderreportedZipEntryFailsVerifyAndInstallWithoutMaterialization()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = workspace.PathFor("managed");
        const string version = "0.10.6";
        UpdateCatalogVersionSnapshot package = CreatePackage(
            sourceRoot,
            version,
            mutateManifest: static manifest =>
            {
                JsonObject readme = manifest["files"]!.AsArray()
                    .Select(node => node!.AsObject())
                    .Single(file => file["path"]!.GetValue<string>() == "README.txt");
                readme["size"] = 1;
            },
            mutatePackage: packagePath => UnderreportZipEntry(
                packagePath,
                $"NvtFwCombiner-v{version}-win-x64/README.txt",
                declaredLength: 1));
        var repository = new FileSystemManagedVersionRepository();

        ManagedPackageVerificationResult verified = await repository.VerifyPackageAsync(
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);
        ManagedVersionInstallResult installed = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.False(verified.IsVerified);
        Assert.Equal(ManagedVersionInstallIssue.InvalidPayload, verified.Issue);
        Assert.Equal(ManagedVersionInstallIssue.InvalidPayload, installed.Issue);
        Assert.False(Directory.Exists(Path.Combine(managedRoot, "versions", version)));
        Assert.False(Directory.Exists(Path.Combine(managedRoot, ".staging")) &&
                     Directory.EnumerateFileSystemEntries(Path.Combine(managedRoot, ".staging")).Any());
    }

    /// <summary>Inventory uses one actual-byte aggregate budget for the complete installed tree.</summary>
    [Fact]
    public async Task InstalledInventoryFailsClosedWhenActualAggregateCrossesCeiling()
    {
        Assert.Equal(512L * 1024 * 1024, FileSystemManagedVersionRepository.MaximumExpandedBytes);
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = workspace.PathFor("managed");
        UpdateCatalogVersionSnapshot package = CreatePackage(sourceRoot, "0.10.6");
        var productionRepository = new FileSystemManagedVersionRepository();
        ManagedVersionInstallResult installed = await productionRepository.InstallAsync(
            managedRoot,
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);
        ManagedVersionAdmission admission = Assert.IsType<ManagedVersionAdmission>(installed.Admission);
        string versionRoot = Path.Combine(managedRoot, "versions", "0.10.6");
        long actualExpandedBytes = Directory.EnumerateFiles(versionRoot, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != FileSystemManagedVersionRepository.AdmissionFileName)
            .Sum(path => new FileInfo(path).Length);
        var constrainedRepository = new FileSystemManagedVersionRepository(actualExpandedBytes - 1);

        ManagedVersionInventory healthy = RequireInventory(await productionRepository.InventoryAsync(
            managedRoot,
            [admission],
            activeVersion: null,
            lastKnownGoodVersion: null,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken));
        ManagedVersionInventory constrained = RequireInventory(await constrainedRepository.InventoryAsync(
            managedRoot,
            [admission],
            activeVersion: null,
            lastKnownGoodVersion: null,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken));

        Assert.Equal(ManagedVersionIntegrity.Healthy, healthy.Find(admission.Version)?.Integrity);
        Assert.Equal(ManagedVersionIntegrity.Damaged, constrained.Find(admission.Version)?.Integrity);
        Assert.Equal(
            ManagedVersionDamageReason.ContentMismatch,
            constrained.Find(admission.Version)?.DamageReason);
    }

    /// <summary>ZIP64 metadata cannot overflow the production aggregate admission check.</summary>
    [Fact]
    public async Task Zip64DeclaredSizeOverflowFailsVerifyAndInstallWithoutResidue()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = workspace.PathFor("managed");
        UpdateCatalogVersionSnapshot package = CreatePackage(
            sourceRoot,
            "0.10.6",
            mutatePackage: InflateSecondCentralEntryToLongMax);
        string packagePath = Path.Combine(
            sourceRoot,
            package.PackagePath.Value.Replace('/', Path.DirectorySeparatorChar));
        using (ZipArchive archive = ZipFile.OpenRead(packagePath))
        {
            Assert.True(archive.Entries[0].Length > 0);
            Assert.Equal(long.MaxValue, archive.Entries[1].Length);
        }
        var repository = new FileSystemManagedVersionRepository();

        ManagedPackageVerificationResult verified = await repository.VerifyPackageAsync(
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);
        ManagedVersionInstallResult installed = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInstallIssue.UnsafeArchive, verified.Issue);
        Assert.Equal(ManagedVersionInstallIssue.UnsafeArchive, installed.Issue);
        Assert.False(Directory.Exists(Path.Combine(managedRoot, "versions", "0.10.6")));
        Assert.False(Directory.Exists(Path.Combine(managedRoot, ".staging")) &&
                     Directory.EnumerateFileSystemEntries(Path.Combine(managedRoot, ".staging")).Any());
    }

    private static void UnderreportZipEntry(
        string packagePath,
        string entryPath,
        uint declaredLength)
    {
        byte[] bytes = File.ReadAllBytes(packagePath);
        ReadOnlySpan<byte> endSignature = [0x50, 0x4b, 0x05, 0x06];
        int end = bytes.AsSpan().LastIndexOf(endSignature);
        Assert.True(end >= 0, "ZIP end-of-central-directory record was not found.");
        int entryCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(end + 10, sizeof(ushort)));
        int central = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
            bytes.AsSpan(end + 16, sizeof(uint))));
        bool found = false;
        for (int index = 0; index < entryCount; index++)
        {
            Assert.Equal(0x02014b50u, BinaryPrimitives.ReadUInt32LittleEndian(
                bytes.AsSpan(central, sizeof(uint))));
            int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(
                bytes.AsSpan(central + 28, sizeof(ushort)));
            int extraLength = BinaryPrimitives.ReadUInt16LittleEndian(
                bytes.AsSpan(central + 30, sizeof(ushort)));
            int commentLength = BinaryPrimitives.ReadUInt16LittleEndian(
                bytes.AsSpan(central + 32, sizeof(ushort)));
            string name = Encoding.UTF8.GetString(bytes, central + 46, nameLength);
            if (string.Equals(name, entryPath, StringComparison.Ordinal))
            {
                BinaryPrimitives.WriteUInt32LittleEndian(
                    bytes.AsSpan(central + 24, sizeof(uint)),
                    declaredLength);
                int local = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
                    bytes.AsSpan(central + 42, sizeof(uint))));
                Assert.Equal(0x04034b50u, BinaryPrimitives.ReadUInt32LittleEndian(
                    bytes.AsSpan(local, sizeof(uint))));
                BinaryPrimitives.WriteUInt32LittleEndian(
                    bytes.AsSpan(local + 22, sizeof(uint)),
                    declaredLength);
                found = true;
                break;
            }
            central = checked(central + 46 + nameLength + extraLength + commentLength);
        }

        Assert.True(found, $"ZIP entry '{entryPath}' was not found.");
        File.WriteAllBytes(packagePath, bytes);
    }

    private static void InflateSecondCentralEntryToLongMax(string packagePath)
    {
        byte[] original = File.ReadAllBytes(packagePath);
        ReadOnlySpan<byte> endSignature = [0x50, 0x4b, 0x05, 0x06];
        int oldEnd = original.AsSpan().LastIndexOf(endSignature);
        Assert.True(oldEnd >= 0, "ZIP end-of-central-directory record was not found.");
        int central = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
            original.AsSpan(oldEnd + 16, sizeof(uint))));
        int firstRecordLength = GetCentralRecordLength(original, central);
        int second = checked(central + firstRecordLength);
        Assert.Equal(0x02014b50u, BinaryPrimitives.ReadUInt32LittleEndian(
            original.AsSpan(second, sizeof(uint))));
        int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(
            original.AsSpan(second + 28, sizeof(ushort)));
        int oldExtraLength = BinaryPrimitives.ReadUInt16LittleEndian(
            original.AsSpan(second + 30, sizeof(ushort)));
        int insertion = checked(second + 46 + nameLength + oldExtraLength);
        ReadOnlySpan<byte> zip64Extra =
            [0x01, 0x00, 0x08, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x7f];
        byte[] mutated = new byte[checked(original.Length + zip64Extra.Length)];
        original.AsSpan(0, insertion).CopyTo(mutated);
        zip64Extra.CopyTo(mutated.AsSpan(insertion));
        original.AsSpan(insertion).CopyTo(mutated.AsSpan(insertion + zip64Extra.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(
            mutated.AsSpan(second + 24, sizeof(uint)),
            uint.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(
            mutated.AsSpan(second + 30, sizeof(ushort)),
            checked((ushort)(oldExtraLength + zip64Extra.Length)));
        int newEnd = checked(oldEnd + zip64Extra.Length);
        uint oldCentralSize = BinaryPrimitives.ReadUInt32LittleEndian(
            mutated.AsSpan(newEnd + 12, sizeof(uint)));
        BinaryPrimitives.WriteUInt32LittleEndian(
            mutated.AsSpan(newEnd + 12, sizeof(uint)),
            checked(oldCentralSize + (uint)zip64Extra.Length));
        File.WriteAllBytes(packagePath, mutated);
    }

    private static int GetCentralRecordLength(byte[] bytes, int offset)
    {
        Assert.Equal(0x02014b50u, BinaryPrimitives.ReadUInt32LittleEndian(
            bytes.AsSpan(offset, sizeof(uint))));
        int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(
            bytes.AsSpan(offset + 28, sizeof(ushort)));
        int extraLength = BinaryPrimitives.ReadUInt16LittleEndian(
            bytes.AsSpan(offset + 30, sizeof(ushort)));
        int commentLength = BinaryPrimitives.ReadUInt16LittleEndian(
            bytes.AsSpan(offset + 32, sizeof(ushort)));
        return checked(46 + nameLength + extraLength + commentLength);
    }
}
