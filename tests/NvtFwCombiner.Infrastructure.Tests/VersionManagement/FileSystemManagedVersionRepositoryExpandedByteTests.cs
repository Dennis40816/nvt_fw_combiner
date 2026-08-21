using System.Buffers.Binary;
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

        ManagedVersionInventory healthy = await productionRepository.InventoryAsync(
            managedRoot,
            [admission],
            activeVersion: null,
            lastKnownGoodVersion: null,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken);
        ManagedVersionInventory constrained = await constrainedRepository.InventoryAsync(
            managedRoot,
            [admission],
            activeVersion: null,
            lastKnownGoodVersion: null,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionIntegrity.Healthy, healthy.Find(admission.Version)?.Integrity);
        Assert.Equal(ManagedVersionIntegrity.Damaged, constrained.Find(admission.Version)?.Integrity);
        Assert.Equal(
            ManagedVersionDamageReason.ContentMismatch,
            constrained.Find(admission.Version)?.DamageReason);
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
}
