using System.IO.Compression;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

internal interface IWindowsCustodiedManagedVersionRepository : IManagedVersionRepository
{
    ValueTask<ManagedVersionPayloadMaterializationResult> MaterializeVerifiedPayloadWithinHeldRootAsync(
        WindowsStableRelativeWriteRoot writeRoot,
        string sourceRoot,
        UpdateCatalogVersionSnapshot package,
        Action<string>? afterPackageDirectoryCreated,
        CancellationToken cancellationToken);
}

internal sealed record ManagedVersionPayloadMaterializationResult(
    ManagedVersionAdmission? Admission,
    ManagedVersionInstallIssue Issue,
    bool WasAlreadyMaterialized)
{
    internal bool IsVerified => Admission is not null && Issue == ManagedVersionInstallIssue.None;
}

internal enum ManagedPayloadTargetPolicy
{
    AdmitMatchingExisting,
    RequireNew,
}

public sealed partial class FileSystemManagedVersionRepository
{
    /// <inheritdoc />
    public async ValueTask<ManagedVersionInstallResult> InstallAsync(
        string managedRoot,
        string sourceRoot,
        UpdateCatalogVersionSnapshot package,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentNullException.ThrowIfNull(package);
        WindowsStableRelativeWriteRoot? ownedRoot = null;
        try
        {
            WindowsStableCustodyIssue acquired = WindowsStableRelativeWriteRoot.TryAcquire(
                managedRoot,
                out WindowsStableRelativeWriteRoot? writeRoot);
            ownedRoot = writeRoot;
            if (acquired != WindowsStableCustodyIssue.None || ownedRoot is null)
            {
                return Failure(ManagedVersionInstallIssue.PromotionFailed);
            }
            ManagedVersionPayloadMaterializationResult payload =
                await MaterializeVerifiedPayloadWithinHeldRootAsync(
                    ownedRoot,
                    sourceRoot,
                    package,
                    _afterPackageDirectoryCreated,
                    ManagedPayloadTargetPolicy.AdmitMatchingExisting,
                    cancellationToken).ConfigureAwait(false);
            return payload.IsVerified
                ? new(
                    payload.Admission,
                    ManagedVersionInstallIssue.None,
                    payload.WasAlreadyMaterialized)
                : Failure(payload.Issue);
        }
        finally
        {
            ownedRoot?.Dispose();
        }
    }

    ValueTask<ManagedVersionPayloadMaterializationResult>
        IWindowsCustodiedManagedVersionRepository.MaterializeVerifiedPayloadWithinHeldRootAsync(
            WindowsStableRelativeWriteRoot writeRoot,
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            Action<string>? afterPackageDirectoryCreated,
            CancellationToken cancellationToken)
    {
        return MaterializeVerifiedPayloadWithinHeldRootAsync(
            writeRoot,
            sourceRoot,
            package,
            afterPackageDirectoryCreated,
            ManagedPayloadTargetPolicy.RequireNew,
            cancellationToken);
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "The created write tree transfers immediately to ownedTree and every path disposes or cleans it in the enclosing finally block.")]
    private async ValueTask<ManagedVersionPayloadMaterializationResult>
        MaterializeVerifiedPayloadWithinHeldRootAsync(
        WindowsStableRelativeWriteRoot writeRoot,
        string sourceRoot,
        UpdateCatalogVersionSnapshot package,
        Action<string>? afterPackageDirectoryCreated,
        ManagedPayloadTargetPolicy targetPolicy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writeRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        ArgumentNullException.ThrowIfNull(package);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            string source = Path.GetFullPath(sourceRoot);
            if (!ManagedPathSafety.IsSafeExistingDirectory(source) ||
                !ManagedPathSafety.TryResolveRelativeFile(
                    source,
                    package.PackagePath.Value,
                    out string packagePath))
            {
                return PayloadFailure(ManagedVersionInstallIssue.PackageUnavailable);
            }

            await using FileStream packageStream = OpenStablePackage(packagePath, package.PackageSize);
            string actualPackageHash = await HashAsync(packageStream, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actualPackageHash, package.PackageSha256, StringComparison.Ordinal))
            {
                return PayloadFailure(ManagedVersionInstallIssue.PackageMismatch);
            }
            packageStream.Position = 0;
            using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true);
            ManagedPackagePlanResult planResult = await ManagedPackageVerifier.CreatePlanAsync(
                archive,
                package,
                _maximumExpandedBytes,
                cancellationToken).ConfigureAwait(false);
            if (!planResult.IsSuccess)
            {
                return PayloadFailure(planResult.Issue);
            }

            ManagedPackagePlan plan = planResult.Plan!;
            var admission = new ManagedVersionAdmission(
                package.Version,
                package.Identity,
                package.ReleaseManifestSha256);
            byte[] admissionBytes = SerializeAdmission(admission);
            string versionsRoot = Path.Combine(writeRoot.RootPath, VersionsDirectoryName);
            string target = ManagedPathSafety.GetExactVersionDirectory(versionsRoot, package.Version);
            if (Directory.Exists(target))
            {
                return targetPolicy == ManagedPayloadTargetPolicy.AdmitMatchingExisting
                    ? await VerifyExistingPayloadAsync(
                        target,
                        admission,
                        wasAlreadyMaterialized: true,
                        heldCustody: null,
                        cancellationToken).ConfigureAwait(false)
                    : PayloadFailure(ManagedVersionInstallIssue.IdentityConflict);
            }

            var reservation = new WindowsStableTreeReservation(
                checked(plan.FileCount + 1),
                plan.ImplicitDirectoryCount,
                checked(plan.ExpandedBytes + admissionBytes.LongLength),
                WindowsStableTreeLimits.ForInstalledVersion(_maximumExpandedBytes));
            WindowsStableRelativeWriteTree? ownedTree = null;
            WindowsStableRelativeWriteTree? createdTree = null;
            try
            {
                WindowsStableCustodyIssue created = writeRoot.TryCreateVersionTree(
                    package.Version.ToString(),
                    reservation,
                    afterPackageDirectoryCreated,
                    out createdTree);
                ownedTree = createdTree;
                createdTree = null;
                if (created != WindowsStableCustodyIssue.None || ownedTree is null)
                {
                    ManagedVersionInstallIssue issue = created == WindowsStableCustodyIssue.InvalidPath
                        ? ManagedVersionInstallIssue.UnsafeArchive
                        : ManagedVersionInstallIssue.PromotionFailed;
                    return ownedTree is null
                        ? PayloadFailure(issue)
                        : PayloadFailureAfterCleanup(ownedTree, issue);
                }
                await ManagedPackageVerifier.ExtractAsync(
                    plan,
                    ownedTree.CreateFile,
                    _maximumExpandedBytes,
                    cancellationToken).ConfigureAwait(false);
                await WriteAdmissionAsync(
                    ownedTree.CreateFile,
                    admissionBytes,
                    cancellationToken).ConfigureAwait(false);
                WindowsStableCustodyIssue prepared = ownedTree.PrepareForPromotion(
                    _beforePackagePromotion);
                if (prepared != WindowsStableCustodyIssue.None)
                {
                    return PayloadFailureAfterCleanup(
                        ownedTree,
                        ManagedVersionInstallIssue.InvalidPayload);
                }
                WindowsStableCustodyIssue promoted = ownedTree.Promote();
                if (promoted != WindowsStableCustodyIssue.None)
                {
                    return PayloadFailureAfterCleanup(
                        ownedTree,
                        promoted == WindowsStableCustodyIssue.Changed
                            ? ManagedVersionInstallIssue.IdentityConflict
                            : ManagedVersionInstallIssue.PromotionFailed);
                }
                WindowsStableCustodyResult captured = ownedTree.CapturePromotedImmutableTree(
                    target,
                    cancellationToken);
                if (!captured.IsAcquired)
                {
                    return PayloadFailureAfterRollback(
                        ownedTree,
                        ManagedVersionInstallIssue.IdentityConflict);
                }
                ManagedVersionPayloadMaterializationResult verified;
                using (WindowsStablePathCustody custody = captured.Custody!)
                {
                    verified = await VerifyExistingPayloadAsync(
                        target,
                        admission,
                        wasAlreadyMaterialized: false,
                        custody,
                        cancellationToken).ConfigureAwait(false);
                }
                if (!verified.IsVerified)
                {
                    return PayloadFailureAfterRollback(ownedTree, verified.Issue);
                }
                ownedTree.Dispose();
                ownedTree = null;
                return verified;
            }
            catch (OperationCanceledException)
            {
                if (ownedTree is not null &&
                    ownedTree.RollbackPromotionAndCleanup() != WindowsStableCustodyIssue.None)
                {
                    return PayloadFailure(ManagedVersionInstallIssue.CleanupIncomplete);
                }
                throw;
            }
            catch (Exception exception) when (exception is
                IOException or UnauthorizedAccessException or InvalidDataException)
            {
                if (ownedTree is not null &&
                    ownedTree.RollbackPromotionAndCleanup() != WindowsStableCustodyIssue.None)
                {
                    return PayloadFailure(ManagedVersionInstallIssue.CleanupIncomplete);
                }
                throw;
            }
            finally
            {
                createdTree?.Dispose();
                ownedTree?.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            return PayloadFailure(ManagedVersionInstallIssue.PackageUnavailable);
        }
        catch (InvalidDataException)
        {
            return PayloadFailure(ManagedVersionInstallIssue.InvalidPayload);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return PayloadFailure(ManagedVersionInstallIssue.PromotionFailed);
        }
    }

    private static ManagedVersionPayloadMaterializationResult PayloadFailureAfterCleanup(
        WindowsStableRelativeWriteTree tree,
        ManagedVersionInstallIssue issue)
    {
        return PayloadFailure(tree.Cleanup() == WindowsStableCustodyIssue.None
            ? issue
            : ManagedVersionInstallIssue.CleanupIncomplete);
    }

    private static ManagedVersionPayloadMaterializationResult PayloadFailureAfterRollback(
        WindowsStableRelativeWriteTree tree,
        ManagedVersionInstallIssue issue)
    {
        return PayloadFailure(tree.RollbackPromotionAndCleanup() == WindowsStableCustodyIssue.None
            ? issue
            : ManagedVersionInstallIssue.CleanupIncomplete);
    }

    private static ManagedVersionPayloadMaterializationResult PayloadFailure(
        ManagedVersionInstallIssue issue)
    {
        return new(null, issue, WasAlreadyMaterialized: false);
    }

    private async ValueTask<ManagedVersionPayloadMaterializationResult> VerifyExistingPayloadAsync(
        string target,
        ManagedVersionAdmission admission,
        bool wasAlreadyMaterialized,
        WindowsStablePathCustody? heldCustody,
        CancellationToken cancellationToken)
    {
        WindowsStablePathCustody? acquiredCustody = null;
        if (heldCustody is null)
        {
            WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireImmutableTree(
                target,
                treeLimits: WindowsStableTreeLimits.ForInstalledVersion(_maximumExpandedBytes),
                cancellationToken: cancellationToken);
            if (!acquired.IsAcquired)
            {
                return PayloadFailure(ManagedVersionInstallIssue.IdentityConflict);
            }
            acquiredCustody = acquired.Custody!;
            heldCustody = acquiredCustody;
        }
        try
        {
            ManagedVersionAdmission? installedAdmission = await ReadAdmissionAsync(
                target,
                cancellationToken).ConfigureAwait(false);
            if (installedAdmission != admission)
            {
                return PayloadFailure(ManagedVersionInstallIssue.IdentityConflict);
            }
            ManagedVersionDamageReason? damage = await ManagedPackageVerifier.VerifyInstalledAsync(
                target,
                admission,
                _maximumExpandedBytes,
                cancellationToken).ConfigureAwait(false);
            return damage is null && heldCustody.RevalidateClosedTree()
                ? new(admission, ManagedVersionInstallIssue.None, wasAlreadyMaterialized)
                : PayloadFailure(ManagedVersionInstallIssue.IdentityConflict);
        }
        finally
        {
            acquiredCustody?.Dispose();
        }
    }

    private static byte[] SerializeAdmission(ManagedVersionAdmission admission)
    {
        var document = new ManagedVersionAdmissionFileDocument(
            admission.Version.ToString(),
            admission.AdmissionIdentity,
            admission.ReleaseManifestSha256);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            document,
            AdmissionJsonContext.ManagedVersionAdmissionFileDocument);
        return bytes.Length <= MaximumAdmissionBytes
            ? bytes
            : throw new InvalidDataException("Managed admission metadata exceeded its declared bound.");
    }

    private static async ValueTask WriteAdmissionAsync(
        Func<string, FileStream> createDestination,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        await using FileStream destination = createDestination(AdmissionFileName);
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
