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
            UpdateSourceRegistryEntry? selectedAuthorityEntry = null;
            UpdateCatalogSnapshot? selectedAuthorityCatalog = null;
            ManagedAppVersion currentVersion = initial.State.ActiveVersion ?? _currentAppVersion;
            foreach (UpdateSourceRegistryEntry entry in registrySnapshot.AutomaticCandidates())
            {
                RegistryCandidateInspection inspection = isAutomatic
                    ? await InspectAutomaticCandidateAsync(
                        entry,
                        registrySnapshot.CatalogPublication,
                        currentVersion,
                        ownedToken).ConfigureAwait(false)
                    : await InspectCandidateAsync(
                        entry,
                        registrySnapshot.CatalogPublication,
                        ownedToken).ConfigureAwait(false);
                selected = inspection.Admission;
                if (selected is not null)
                {
                    break;
                }
                if (isAutomatic &&
                    inspection.Catalog is { } catalog &&
                    catalog.FindNewestNotifyNewerThan(currentVersion) is null)
                {
                    selectedAuthorityEntry = entry;
                    selectedAuthorityCatalog = catalog;
                    break;
                }
            }
            if (selected is null && selectedAuthorityCatalog is null)
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
                    !initial.State.CreateDurableSnapshotToken().Matches(
                        durable.State.CreateDurableSnapshotToken()) ||
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

                UpdateSourceRegistryEntry selectedEntry = selected?.Entry ?? selectedAuthorityEntry!;
                UpdateSourceRegistryEntry? reloadedEntry = reloadedRegistry.Snapshot
                    .AutomaticCandidates()
                    .SingleOrDefault(entry =>
                        entry.Status == selectedEntry.Status &&
                        SourcePathEquals(entry.CatalogPath, selectedEntry.CatalogPath));
                if (selected is null)
                {
                    RegistryCandidateInspection authorityReadmission = reloadedEntry is null
                        ? new(Attempt: default!, Admission: null, Catalog: null)
                        : await InspectCatalogPublicationAsync(
                            reloadedEntry,
                            reloadedRegistry.Snapshot.CatalogPublication,
                            ownedToken).ConfigureAwait(false);
                    if (authorityReadmission.Catalog is not { } readmittedCatalog ||
                        authorityReadmission.Admission is not null ||
                        readmittedCatalog.FindNewestNotifyNewerThan(currentVersion) is not null ||
                        !CatalogPublicationEquals(selectedAuthorityCatalog!, readmittedCatalog))
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

                    var acceptedAuthority = new VersionSourceRegistryState(
                        registrySnapshot.RegistryRevision,
                        registrySnapshot.ContentDigest,
                        isManualPin: false);
                    bool maySaveAuthority = CanPersistAllManualRegistryAuthority(
                        durable.State,
                        selectedAuthorityEntry!.SourceRoot);
                    bool authorityChanged = maySaveAuthority &&
                        durable.State.SourceRegistryState != acceptedAuthority;
                    VersionManagerState authorityState = authorityChanged
                        ? durable.State.WithUpdateSource(durable.State.UpdateSource!, acceptedAuthority)
                        : durable.State;
                    if (authorityChanged &&
                        !await TrySaveAsync(authorityState, ownedToken).ConfigureAwait(false))
                    {
                        return PublishRegistryStateUnavailable(durable, ownedCancellation);
                    }

                    _current = durable with
                    {
                        State = authorityState,
                        Catalog = null,
                        VerifiedCandidate = null,
                        SourceStatus = VersionSourceStatus.Connected,
                        CatalogIssue = UpdateCatalogLoadIssue.None,
                        Generation = generation,
                        ShouldPromptForUpdate = false,
                        RegistryStatus = selectedAuthorityEntry.Status == UpdateSourceRegistryEntryStatus.Latest
                            ? VersionRegistryStatus.LatestSelected
                            : VersionRegistryStatus.FallbackSelected,
                        RegistryIssue = UpdateSourceRegistryIssue.None,
                    };
                    CompleteOwnedCheck(ownedCancellation);
                    return _current;
                }
                RegistryCandidateAdmission? readmitted = reloadedEntry is null
                    ? null
                    : (isAutomatic
                        ? await InspectAutomaticCandidateAsync(
                            reloadedEntry,
                            reloadedRegistry.Snapshot.CatalogPublication,
                            currentVersion,
                            ownedToken).ConfigureAwait(false)
                        : await InspectCandidateAsync(
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
                    Catalog = isAutomatic && visibleCandidate is not null
                        ? new([selected.NewestPackage])
                        : selected.Catalog,
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
        RegistryCandidateInspection catalogInspection = await InspectCatalogPublicationAsync(
            entry,
            expectedPublication,
            cancellationToken).ConfigureAwait(false);
        return catalogInspection.Catalog is not { } catalog
            ? catalogInspection
            : await VerifyRegistryPackageAsync(
                entry,
                catalog,
                catalog.Versions[0],
                cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<RegistryCandidateInspection> InspectAutomaticCandidateAsync(
        UpdateSourceRegistryEntry entry,
        UpdateCatalogPublicationAssertion expectedPublication,
        ManagedAppVersion currentVersion,
        CancellationToken cancellationToken)
    {
        RegistryCandidateInspection catalogInspection = await InspectCatalogPublicationAsync(
            entry,
            expectedPublication,
            cancellationToken).ConfigureAwait(false);
        if (catalogInspection.Catalog is not { } catalog)
        {
            return catalogInspection;
        }
        UpdateCatalogVersionSnapshot? selectedPackage =
            catalog.FindNewestNotifyNewerThan(currentVersion);
        return selectedPackage is null
            ? catalogInspection
            : await VerifyRegistryPackageAsync(
                entry,
                catalog,
                selectedPackage,
                cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<RegistryCandidateInspection> InspectCatalogPublicationAsync(
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
                Admission: null,
                Catalog: null);
        }
        UpdateCatalogSnapshot admittedCatalog = loaded.Snapshot ??
            throw new InvalidOperationException("Matched Catalog publication has no snapshot.");
        return new(
            new(
                entry.SourceRoot,
                entry.Status,
                UpdateCatalogLoadIssue.None,
                packageIssue: ManagedVersionInstallIssue.InvalidPayload,
                newestVersion: admittedCatalog.Versions[0].Version,
                isVerified: false),
            Admission: null,
            Catalog: admittedCatalog);
    }

    private async ValueTask<RegistryCandidateInspection> VerifyRegistryPackageAsync(
        UpdateSourceRegistryEntry entry,
        UpdateCatalogSnapshot catalog,
        UpdateCatalogVersionSnapshot selectedPackage,
        CancellationToken cancellationToken)
    {
        ManagedPackageVerificationResult verification = await _repository.VerifyPackageAsync(
            entry.SourceRoot,
            selectedPackage,
            cancellationToken).ConfigureAwait(false);
        bool verified = verification is { IsVerified: true, Candidate: { } candidate } &&
            candidate.Version == selectedPackage.Version &&
            string.Equals(candidate.AdmissionIdentity, selectedPackage.Identity, StringComparison.Ordinal);
        ManagedVersionInstallIssue packageIssue = verified
            ? ManagedVersionInstallIssue.None
            : verification.Issue == ManagedVersionInstallIssue.None
                ? ManagedVersionInstallIssue.InvalidPayload
                : verification.Issue;
        RegistryCandidateAdmission? admission = !verified
            ? null
            : new(
                entry,
                catalog,
                selectedPackage,
                verification.Candidate!,
                verification.HasSupportedManagedLauncher);
        return new(
            new(
                entry.SourceRoot,
                entry.Status,
                UpdateCatalogLoadIssue.None,
                packageIssue,
                selectedPackage.Version,
                verified),
            admission,
            catalog);
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

    internal static bool CanPersistAllManualRegistryAuthority(
        VersionManagerState state,
        string selectedSourceRoot)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedSourceRoot);
        return state.UpdateSource is { } effectiveSource &&
            state.SourceRegistryState is { IsManualPin: false } &&
            SourcePathEquals(selectedSourceRoot, effectiveSource);
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
                !string.Equals(first.ReleaseNotes, second.ReleaseNotes, StringComparison.Ordinal) ||
                first.NotificationPolicy != second.NotificationPolicy)
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
        RegistryCandidateAdmission? Admission,
        UpdateCatalogSnapshot? Catalog);
}
