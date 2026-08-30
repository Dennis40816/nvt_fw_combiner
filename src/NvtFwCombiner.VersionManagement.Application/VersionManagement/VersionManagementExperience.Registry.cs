namespace NvtFwCombiner.Application.VersionManagement;

public sealed partial class VersionManagementExperience
{
    /// <inheritdoc />
    public async ValueTask<VersionManagementSnapshot> ResumeRegistryAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (_sourceRegistry is null)
        {
            await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                VersionManagementSnapshot current = await RequireCurrentWithoutLockAsync(cancellationToken)
                    .ConfigureAwait(false);
                _current = current with
                {
                    RegistryStatus = VersionRegistryStatus.NotConfigured,
                    RegistryIssue = UpdateSourceRegistryIssue.NotConfigured,
                };
                return _current;
            }
            finally
            {
                _ = _mutation.Release();
            }
        }

        return await CheckRegistryAsync(
            isAutomatic: false,
            allowManualPin: true,
            cancellationToken).ConfigureAwait(false) ??
            throw new InvalidOperationException("Configured registry resolution returned no result.");
    }

    private async ValueTask<VersionManagementSnapshot?> CheckRegistryAsync(
        bool isAutomatic,
        bool allowManualPin,
        CancellationToken cancellationToken)
    {
        IUpdateSourceRegistry registry = _sourceRegistry ??
            throw new InvalidOperationException("Registry resolution requires a configured port.");
        VersionManagementSnapshot initial;
        long generation;
        CancellationTokenSource ownedCancellation;
        CancellationToken ownedToken;
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            initial = await RequireCurrentWithoutLockAsync(cancellationToken).ConfigureAwait(false);
            if (!HasUsableAuthority(initial) || initial.State is null)
            {
                return initial;
            }
            if (initial.State.SourceRegistryState?.IsManualPin == true && !allowManualPin)
            {
                _current = initial with
                {
                    RegistryStatus = VersionRegistryStatus.ManualPin,
                    RegistryIssue = UpdateSourceRegistryIssue.None,
                };
                return null;
            }
            lock (_generationSync)
            {
                generation = _session.BeginCheck();
                CancelRunningCheckUnderLock();
                _checkCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                ownedCancellation = _checkCancellation;
                ownedToken = ownedCancellation.Token;
                _current = initial with
                {
                    SourceStatus = VersionSourceStatus.Checking,
                    CatalogIssue = null,
                    Generation = generation,
                    ShouldPromptForUpdate = false,
                };
            }
        }
        finally
        {
            _ = _mutation.Release();
        }

        try
        {
            UpdateSourceRegistryLoadResult loaded = await registry.LoadAsync(ownedToken)
                .ConfigureAwait(false);
            if (!loaded.IsSuccess)
            {
                return PublishRegistryFailure(
                    initial,
                    generation,
                    ownedCancellation,
                    ResolveLoadStatus(loaded.Issue),
                    ResolveLoadIssue(loaded.Issue));
            }
            UpdateSourceRegistrySnapshot registrySnapshot = loaded.Snapshot!;
            if (ValidateAntiRollback(initial.State!.SourceRegistryState, registrySnapshot) is { } authorityIssue)
            {
                return PublishRegistryFailure(
                    initial,
                    generation,
                    ownedCancellation,
                    VersionRegistryStatus.Rejected,
                    authorityIssue);
            }

            RegistryCandidateAdmission? selected = null;
            foreach (UpdateSourceRegistryEntry entry in registrySnapshot.AutomaticCandidates())
            {
                RegistryCandidateInspection inspection = await InspectCandidateAsync(
                    entry,
                    registrySnapshot.CatalogPublication,
                    ownedToken).ConfigureAwait(false);
                selected = inspection.Admission;
                if (selected is not null)
                {
                    break;
                }
            }
            if (selected is null)
            {
                bool deprecated = initial.State.UpdateSource is { } prior &&
                    registrySnapshot.Entries.Any(entry =>
                        entry.Status == UpdateSourceRegistryEntryStatus.Deprecated &&
                        SourcePathEquals(entry.SourceRoot, prior));
                return PublishRegistryFailure(
                    initial,
                    generation,
                    ownedCancellation,
                    deprecated
                        ? VersionRegistryStatus.DeprecatedRetained
                        : VersionRegistryStatus.Exhausted,
                    deprecated
                        ? UpdateSourceRegistryIssue.CurrentSourceDeprecated
                        : UpdateSourceRegistryIssue.CandidatesExhausted);
            }

            await _mutation.WaitAsync(ownedToken).ConfigureAwait(false);
            try
            {
                if (!OwnsCheck(ownedCancellation))
                {
                    return PublishSuperseded(initial, ownedCancellation);
                }
                using VersionManagerWriteLeaseResult lease = await AcquireWriteLeaseAsync(
                    TimeSpan.Zero,
                    ownedToken).ConfigureAwait(false);
                if (!lease.IsAcquired)
                {
                    return PublishRegistryStateUnavailable(initial, ownedCancellation);
                }
                VersionManagementSnapshot durable = await ReloadDurableCurrentWithoutLockAsync(ownedToken)
                    .ConfigureAwait(false);
                if (!HasUsableAuthority(durable) ||
                    durable.State is null ||
                    !SameDurableState(initial.State, durable.State) ||
                    (durable.State.SourceRegistryState?.IsManualPin == true && !allowManualPin) ||
                    await LoadClearLauncherFenceAsync(ownedToken).ConfigureAwait(false) is null)
                {
                    return PublishRegistryStateUnavailable(durable, ownedCancellation);
                }

                UpdateSourceRegistryLoadResult reloadedRegistry = await registry.LoadAsync(ownedToken)
                    .ConfigureAwait(false);
                if (!reloadedRegistry.IsSuccess ||
                    reloadedRegistry.Snapshot!.RegistryRevision != registrySnapshot.RegistryRevision ||
                    !string.Equals(
                        reloadedRegistry.Snapshot.ContentDigest,
                        registrySnapshot.ContentDigest,
                        StringComparison.Ordinal))
                {
                    return PublishRegistryFailure(
                        durable,
                        generation,
                        ownedCancellation,
                        VersionRegistryStatus.Rejected,
                        UpdateSourceRegistryIssue.RegistryChanged);
                }
                if (ValidateAntiRollback(durable.State.SourceRegistryState, reloadedRegistry.Snapshot) is { } reloadedIssue)
                {
                    return PublishRegistryFailure(
                        durable,
                        generation,
                        ownedCancellation,
                        VersionRegistryStatus.Rejected,
                        reloadedIssue);
                }

                UpdateSourceRegistryEntry? reloadedEntry = reloadedRegistry.Snapshot
                    .AutomaticCandidates()
                    .SingleOrDefault(entry =>
                        entry.Status == selected.Entry.Status &&
                        SourcePathEquals(entry.CatalogPath, selected.Entry.CatalogPath));
                RegistryCandidateAdmission? readmitted = reloadedEntry is null
                    ? null
                    : (await InspectCandidateAsync(
                        reloadedEntry,
                        reloadedRegistry.Snapshot.CatalogPublication,
                        ownedToken).ConfigureAwait(false)).Admission;
                if (readmitted is null ||
                    !CatalogPublicationEquals(selected.Catalog, readmitted.Catalog) ||
                    selected.NewestPackage.Identity != readmitted.NewestPackage.Identity ||
                    selected.VerifiedCandidate != readmitted.VerifiedCandidate)
                {
                    return PublishRegistryFailure(
                        durable,
                        generation,
                        ownedCancellation,
                        VersionRegistryStatus.Rejected,
                        UpdateSourceRegistryIssue.RegistryChanged);
                }
                if (await LoadClearLauncherFenceAsync(ownedToken).ConfigureAwait(false) is null)
                {
                    return PublishRegistryStateUnavailable(durable, ownedCancellation);
                }

                var acceptedRegistry = new VersionSourceRegistryState(
                    registrySnapshot.RegistryRevision,
                    registrySnapshot.ContentDigest,
                    isManualPin: false);
                VersionManagerState acceptedState = durable.State.WithUpdateSource(
                    selected.Entry.SourceRoot,
                    acceptedRegistry);
                bool stateChanged = !SourcePathEquals(
                        durable.State.UpdateSource,
                        acceptedState.UpdateSource) ||
                    durable.State.SourceRegistryState != acceptedRegistry;
                if (stateChanged &&
                    !await TrySaveAsync(acceptedState, ownedToken).ConfigureAwait(false))
                {
                    return PublishRegistryStateUnavailable(durable, ownedCancellation);
                }

                ManagedAppVersion active = acceptedState.ActiveVersion ?? _currentAppVersion;
                VerifiedUpdateCandidate? visibleCandidate = selected.NewestPackage.Version > active
                    ? selected.VerifiedCandidate
                    : null;
                bool shouldPrompt = isAutomatic && visibleCandidate is not null &&
                    _session.TryPublishAutomaticPrompt(generation, active, visibleCandidate);
                _current = durable with
                {
                    State = stateChanged ? acceptedState : durable.State,
                    Catalog = selected.Catalog,
                    VerifiedCandidate = visibleCandidate,
                    SourceStatus = VersionSourceStatus.Connected,
                    CatalogIssue = UpdateCatalogLoadIssue.None,
                    Generation = generation,
                    ShouldPromptForUpdate = shouldPrompt,
                    RegistryStatus = selected.Entry.Status == UpdateSourceRegistryEntryStatus.Latest
                        ? VersionRegistryStatus.LatestSelected
                        : VersionRegistryStatus.FallbackSelected,
                    RegistryIssue = UpdateSourceRegistryIssue.None,
                };
                CompleteOwnedCheck(ownedCancellation);
                return _current;
            }
            finally
            {
                _ = _mutation.Release();
            }
        }
        catch (OperationCanceledException) when (
            ownedToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            lock (_generationSync)
            {
                return RequirePublishedSnapshotUnderLock() with
                {
                    RegistryIssue = UpdateSourceRegistryIssue.Superseded,
                };
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RestoreCancelledCheck(initial, ownedCancellation);
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<VersionEnvironmentSelfTestResult> RunEnvironmentSelfTestAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (_sourceRegistry is null)
        {
            return new(UpdateSourceRegistryLoadIssue.NotConfigured, []);
        }

        UpdateSourceRegistryLoadResult registry = await _sourceRegistry.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!registry.IsSuccess)
        {
            return new(registry.Issue, [], registry.Replicas);
        }

        (VersionSourceRegistryState? acceptedAuthority, UpdateSourceRegistryIssue? authorityIssue) =
            await ReadAndValidateRegistryAuthorityAsync(
                registry.Snapshot!,
                cancellationToken).ConfigureAwait(false);
        if (authorityIssue is { } rejected)
        {
            return new(
                UpdateSourceRegistryLoadIssue.None,
                [],
                registry.Replicas,
                rejected,
                acceptedAuthority?.AcceptedRevision);
        }

        var attempts = new List<VersionEnvironmentSelfTestAttempt>(
            registry.Snapshot!.Entries.Count);
        foreach (UpdateSourceRegistryEntry entry in registry.Snapshot.AutomaticCandidates())
        {
            RegistryCandidateInspection inspection = await InspectCandidateAsync(
                entry,
                registry.Snapshot.CatalogPublication,
                cancellationToken).ConfigureAwait(false);
            attempts.Add(inspection.Attempt);
        }
        (acceptedAuthority, authorityIssue) = await ReadAndValidateRegistryAuthorityAsync(
            registry.Snapshot,
            cancellationToken).ConfigureAwait(false);
        return authorityIssue is { } changedAuthority
            ? new(
                UpdateSourceRegistryLoadIssue.None,
                [],
                registry.Replicas,
                changedAuthority,
                acceptedAuthority?.AcceptedRevision)
            : new(
                UpdateSourceRegistryLoadIssue.None,
                attempts,
                registry.Replicas,
                acceptedRegistryRevision: acceptedAuthority?.AcceptedRevision);
    }

    private async ValueTask<(
        VersionSourceRegistryState? Accepted,
        UpdateSourceRegistryIssue? Issue)> ReadAndValidateRegistryAuthorityAsync(
        UpdateSourceRegistrySnapshot candidate,
        CancellationToken cancellationToken)
    {
        VersionManagerStateLoadResult durable = await _stateStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        VersionSourceRegistryState? accepted = durable.IsSuccess
            ? durable.State!.SourceRegistryState
            : null;
        return durable.Issue is not (
                VersionManagerStateLoadIssue.None or VersionManagerStateLoadIssue.Missing)
            ? (accepted, UpdateSourceRegistryIssue.StateUnavailable)
            : (accepted, ValidateAntiRollback(accepted, candidate));
    }

    private async ValueTask<RegistryCandidateInspection> InspectCandidateAsync(
        UpdateSourceRegistryEntry entry,
        UpdateCatalogPublicationAssertion expectedPublication,
        CancellationToken cancellationToken)
    {
        UpdateCatalogLoadResult loaded = await _catalogSource.LoadCatalogAsync(
            entry.CatalogPath,
            cancellationToken).ConfigureAwait(false);
        bool publicationMatches = loaded is
        {
            IsSuccess: true,
            Snapshot.Versions.Count: > 0,
            ContentIdentity: { } identity,
        } &&
            identity.SchemaVersion == expectedPublication.CatalogSchemaVersion &&
            string.Equals(
                identity.Sha256,
                expectedPublication.CatalogSha256,
                StringComparison.Ordinal) &&
            loaded.Snapshot.Versions[0].Version == expectedPublication.LatestVersion;
        if (!publicationMatches)
        {
            return new(
                new(
                    entry.SourceRoot,
                    entry.Status,
                    loaded.IsSuccess ? UpdateCatalogLoadIssue.InvalidManifest : loaded.Issue,
                    packageIssue: null,
                    newestVersion: null,
                    isVerified: false),
                Admission: null);
        }
        UpdateCatalogSnapshot admittedCatalog = loaded.Snapshot ??
            throw new InvalidOperationException("Matched Catalog publication has no snapshot.");
        UpdateCatalogVersionSnapshot newest = admittedCatalog.Versions[0];
        ManagedPackageVerificationResult verification = await _repository.VerifyPackageAsync(
            entry.SourceRoot,
            newest,
            cancellationToken).ConfigureAwait(false);
        bool verified = verification is { IsVerified: true, Candidate: { } candidate } &&
            candidate.Version == newest.Version &&
            string.Equals(candidate.AdmissionIdentity, newest.Identity, StringComparison.Ordinal);
        ManagedVersionInstallIssue packageIssue = verified
            ? ManagedVersionInstallIssue.None
            : verification.Issue == ManagedVersionInstallIssue.None
                ? ManagedVersionInstallIssue.InvalidPayload
                : verification.Issue;
        RegistryCandidateAdmission? admission = !verified
            ? null
            : new(
                entry,
                admittedCatalog,
                newest,
                verification.Candidate!,
                verification.HasSupportedManagedLauncher);
        return new(
            new(
                entry.SourceRoot,
                entry.Status,
                UpdateCatalogLoadIssue.None,
                packageIssue,
                newest.Version,
                verified),
            admission);
    }

    private VersionManagementSnapshot PublishRegistryFailure(
        VersionManagementSnapshot basis,
        long generation,
        CancellationTokenSource ownedCancellation,
        VersionRegistryStatus status,
        UpdateSourceRegistryIssue issue)
    {
        lock (_generationSync)
        {
            if (!ReferenceEquals(_checkCancellation, ownedCancellation))
            {
                return RequirePublishedSnapshotUnderLock();
            }
            _current = basis with
            {
                Catalog = null,
                VerifiedCandidate = null,
                SourceStatus = basis.State?.UpdateSource is null
                    ? VersionSourceStatus.NotConfigured
                    : VersionSourceStatus.Offline,
                CatalogIssue = null,
                Generation = generation,
                ShouldPromptForUpdate = false,
                RegistryStatus = status,
                RegistryIssue = issue,
            };
            CompleteOwnedCheckUnderLock(ownedCancellation);
            return _current;
        }
    }

    private VersionManagementSnapshot PublishRegistryStateUnavailable(
        VersionManagementSnapshot basis,
        CancellationTokenSource? ownedCancellation = null)
    {
        _ = basis;
        VersionManagementSnapshot unavailable = PublishStateUnavailable();
        _current = unavailable with
        {
            RegistryStatus = VersionRegistryStatus.Unavailable,
            RegistryIssue = UpdateSourceRegistryIssue.StateUnavailable,
        };
        if (ownedCancellation is not null)
        {
            CompleteOwnedCheck(ownedCancellation);
        }
        return _current;
    }

    private VersionManagementSnapshot PublishSuperseded(
        VersionManagementSnapshot basis,
        CancellationTokenSource ownedCancellation)
    {
        _ = basis;
        CompleteOwnedCheck(ownedCancellation);
        lock (_generationSync)
        {
            return RequirePublishedSnapshotUnderLock() with
            {
                RegistryIssue = UpdateSourceRegistryIssue.Superseded,
            };
        }
    }

    private void CompleteOwnedCheck(CancellationTokenSource ownedCancellation)
    {
        lock (_generationSync)
        {
            CompleteOwnedCheckUnderLock(ownedCancellation);
        }
    }

    private void CompleteOwnedCheckUnderLock(CancellationTokenSource ownedCancellation)
    {
        if (ReferenceEquals(_checkCancellation, ownedCancellation))
        {
            _checkCancellation = null;
            ownedCancellation.Dispose();
        }
    }

    private bool OwnsCheck(CancellationTokenSource ownedCancellation)
    {
        lock (_generationSync)
        {
            return ReferenceEquals(_checkCancellation, ownedCancellation);
        }
    }

    private void RestoreCancelledCheck(
        VersionManagementSnapshot prior,
        CancellationTokenSource ownedCancellation)
    {
        lock (_generationSync)
        {
            if (ReferenceEquals(_checkCancellation, ownedCancellation))
            {
                _current = prior;
                CompleteOwnedCheckUnderLock(ownedCancellation);
            }
        }
    }

    private static UpdateSourceRegistryIssue? ValidateAntiRollback(
        VersionSourceRegistryState? accepted,
        UpdateSourceRegistrySnapshot candidate)
    {
        return accepted is null || accepted.AcceptedRevision == 0
            ? null
            : candidate.RegistryRevision < accepted.AcceptedRevision
                ? UpdateSourceRegistryIssue.RevisionRollback
                : candidate.RegistryRevision == accepted.AcceptedRevision &&
                  !string.Equals(candidate.ContentDigest, accepted.AcceptedDigest, StringComparison.Ordinal)
                    ? UpdateSourceRegistryIssue.RevisionConflict
                    : null;
    }

    private static bool SameDurableState(VersionManagerState expected, VersionManagerState actual)
    {
        return SourcePathEquals(expected.ManagedRootIdentity, actual.ManagedRootIdentity) &&
            SourcePathEquals(expected.UpdateSource, actual.UpdateSource) &&
            expected.ActiveVersion == actual.ActiveVersion &&
            expected.LastKnownGoodVersion == actual.LastKnownGoodVersion &&
            expected.Admissions.SequenceEqual(actual.Admissions) &&
            expected.PendingActivation == actual.PendingActivation &&
            expected.FailedActivationVersion == actual.FailedActivationVersion &&
            expected.RetentionReviewDue == actual.RetentionReviewDue &&
            expected.PendingMutation == actual.PendingMutation &&
            expected.SourceRegistryState == actual.SourceRegistryState;
    }

    private static bool CatalogPublicationEquals(
        UpdateCatalogSnapshot left,
        UpdateCatalogSnapshot right)
    {
        if (left.Versions.Count != right.Versions.Count)
        {
            return false;
        }
        for (int index = 0; index < left.Versions.Count; index++)
        {
            UpdateCatalogVersionSnapshot first = left.Versions[index];
            UpdateCatalogVersionSnapshot second = right.Versions[index];
            if (first.Version != second.Version ||
                first.PublishedAt != second.PublishedAt ||
                first.PackagePath != second.PackagePath ||
                first.PackageSize != second.PackageSize ||
                !string.Equals(first.PackageSha256, second.PackageSha256, StringComparison.Ordinal) ||
                !string.Equals(
                    first.ReleaseManifestSha256,
                    second.ReleaseManifestSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(first.ReleaseNotes, second.ReleaseNotes, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static VersionRegistryStatus ResolveLoadStatus(UpdateSourceRegistryLoadIssue issue)
    {
        return issue is UpdateSourceRegistryLoadIssue.InvalidManifest or
            UpdateSourceRegistryLoadIssue.UnsafeLocator or
            UpdateSourceRegistryLoadIssue.RegistryTooLarge or
            UpdateSourceRegistryLoadIssue.UnstableRead or
            UpdateSourceRegistryLoadIssue.ReplicaConflict
                ? VersionRegistryStatus.Rejected
                : VersionRegistryStatus.Unavailable;
    }

    private static UpdateSourceRegistryIssue ResolveLoadIssue(UpdateSourceRegistryLoadIssue issue)
    {
        return issue switch
        {
            UpdateSourceRegistryLoadIssue.NotConfigured => UpdateSourceRegistryIssue.NotConfigured,
            UpdateSourceRegistryLoadIssue.PermissionDenied => UpdateSourceRegistryIssue.PermissionDenied,
            UpdateSourceRegistryLoadIssue.AuthenticationRequired =>
                UpdateSourceRegistryIssue.AuthenticationRequired,
            UpdateSourceRegistryLoadIssue.RegistryTimedOut => UpdateSourceRegistryIssue.TimedOut,
            UpdateSourceRegistryLoadIssue.InvalidManifest or
            UpdateSourceRegistryLoadIssue.UnsafeLocator or
            UpdateSourceRegistryLoadIssue.RegistryTooLarge or
            UpdateSourceRegistryLoadIssue.UnstableRead or
            UpdateSourceRegistryLoadIssue.ReplicaConflict => UpdateSourceRegistryIssue.Invalid,
            UpdateSourceRegistryLoadIssue.RegistryMissing or
            UpdateSourceRegistryLoadIssue.RegistryUnavailable => UpdateSourceRegistryIssue.Unavailable,
            UpdateSourceRegistryLoadIssue.None => UpdateSourceRegistryIssue.Invalid,
            _ => UpdateSourceRegistryIssue.Invalid,
        };
    }

    private static bool SourcePathEquals(string? left, string? right)
    {
        return OperatingSystem.IsWindows()
            ? string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
            : string.Equals(left, right, StringComparison.Ordinal);
    }

    private sealed record RegistryCandidateAdmission(
        UpdateSourceRegistryEntry Entry,
        UpdateCatalogSnapshot Catalog,
        UpdateCatalogVersionSnapshot NewestPackage,
        VerifiedUpdateCandidate VerifiedCandidate,
        bool HasSupportedManagedLauncher);

    private sealed record RegistryCandidateInspection(
        VersionEnvironmentSelfTestAttempt Attempt,
        RegistryCandidateAdmission? Admission);
}
