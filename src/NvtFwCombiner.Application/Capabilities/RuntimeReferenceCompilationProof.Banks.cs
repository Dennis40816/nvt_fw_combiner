using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

public sealed partial class RuntimeReferenceCompilationProof
{
    private readonly BankProof[]? _bankProofs;

    private RuntimeReferenceCompilationProof(CompiledComposition composition, BankProof[] proofs)
        : this(composition.CompilationFingerprint, string.Empty, string.Empty, "bank-replace", string.Empty,
            GetSingleProcessor(proofs[0].Parent), [])
    {
        _bankProofs = [.. proofs];
        _ = ValidateBankProof(composition);
    }

    /// <summary>Proves every local processor and its exact binding into the one checked bank plan.</summary>
    public static RuntimeReferenceCompilationProof CreateBankReplace(CompiledComposition composition,
        IReadOnlyDictionary<string, LegacyCombinerPostbuildCommandPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(plans);
        RuntimeReferenceBankReplaceV2CompilationContext context = ValidateBankAssembly(composition);
        _ = plans.Count == context.Banks.Count && context.Banks.All(bank => plans.ContainsKey(bank.BankId))
            ? true : throw new ArgumentException("Every selected bank requires exactly one local postbuild proof.", nameof(plans));

        return new RuntimeReferenceCompilationProof(composition, [.. context.Banks.Select(bank => new BankProof(
            bank.BankId, bank.LocalComposition, CreateLegacyPostbuild(bank.LocalComposition, plans[bank.BankId])))]);
    }

    private IReadOnlyList<string> ValidateBankProof(CompiledComposition composition)
    {
        RuntimeReferenceBankReplaceV2CompilationContext context = ValidateBankAssembly(composition);
        if (composition.CompilationFingerprint != _compilationFingerprint || _bankProofs!.Length != context.Banks.Count)
        {
            throw new ArgumentException("The bank proof does not identify this exact compilation.", nameof(composition));
        }

        var bindings = new List<string> { "bank-definition:" + context.Definition.ContentHash };
        foreach (CompiledReferenceBank bank in context.Banks)
        {
            BankProof proof = _bankProofs.Single(candidate => candidate.BankId == bank.BankId);
            if (!ReferenceEquals(bank.LocalComposition, proof.Parent))
            {
                throw new ArgumentException("A bank cannot substitute another local compilation's proof.", nameof(composition));
            }

            bindings.AddRange(proof.Proof.ValidateAndGetSemanticBindings(bank.LocalComposition));
        }

        return [.. bindings.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    private static RuntimeReferenceBankReplaceV2CompilationContext ValidateBankAssembly(CompiledComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        if (!composition.IsV2BankReplaceCandidate ||
            composition.V2Details.Provenance.Context is not RuntimeReferenceBankReplaceV2CompilationContext context)
        {
            throw new ArgumentException("Bank proof requires the closed checked AB Replace candidate.", nameof(composition));
        }

        var operations = composition.Plan.OrderedOperations.ToDictionary(static operation => operation.OperationId, StringComparer.Ordinal);
        var expectedIds = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<CompiledValidationRequirement> validations = composition.V2Details.Provenance.ValidationRequirements;
        Require(validations.Count == context.Banks.Sum(static bank => bank.LocalComposition.V2Details.Provenance.ValidationRequirements.Count) &&
            validations.All(validation => validation is CompiledBankScopedValidation scoped && context.Banks.Contains(scoped.Bank) &&
                scoped.Bank.LocalComposition.V2Details.Provenance.ValidationRequirements.Contains(scoped.Local)));
        Require(composition.Plan.OutputInitialization.Kind == ImageInitializationKind.Reference &&
            composition.Plan.OutputInitialization.ReferenceSpaceId == context.Reference.ArtifactId &&
            composition.Plan.OutputInitialization.Capacity == context.Reference.LengthBytes &&
            composition.Plan.Initializations.Count == context.Banks.Count + 1);
        int firstProcessor = composition.Plan.OrderedOperations.First(static operation => operation.Kind == CompositionOperationKind.RunExternalProcessor).Sequence;
        CompositionOperation[] relocations = [.. context.LayoutComposition.Plan.OrderedOperations.Where(static operation => operation.Kind == CompositionOperationKind.TransformScalar)];
        bool partial = context.Definition.FinalizationKind == BankReferenceFinalizationKind.RunAbHeaderProcessor;
        CompositionOperation? abFinalizer = partial ? context.LayoutComposition.Plan.OrderedOperations.Single(static operation =>
            operation.Kind == CompositionOperationKind.RunExternalProcessor) : null;
        ByteRange[] headerAddressFields = abFinalizer is null ? [] :
        [
            .. abFinalizer.ExternalProcessorInvocation!.AllowedWriteRanges.Take(2).Select(range =>
                new ByteRange(checked(range.Start - context.Definition.BankCapacityBytes), range.Length)),
        ];
        foreach (CompiledReferenceBank bank in context.Banks)
        {
            var wholeBank = new ByteRange(0, bank.OutputRange.Length);
            ImageInitialization initialization = composition.Plan.Initializations.Single(value => value.TargetSpaceId == bank.WorkspaceId);
            Require(initialization.Kind == ImageInitializationKind.Blank && initialization.Capacity == wholeBank.Length);
            CompositionOperation seed = Take(bank.BankId + "/seed");
            Require(seed.Kind == CompositionOperationKind.CopyRange && seed.SourceSpaceId == bank.Reference.ArtifactId &&
                seed.TargetSpaceId == bank.WorkspaceId && seed.SourceRange == wholeBank && seed.TargetRange == wholeBank &&
                seed.Sequence < firstProcessor);
            CompositionOperation publish = Take(bank.BankId + "/publish");
            Require(publish.Kind == CompositionOperationKind.CopyRange && publish.SourceSpaceId == bank.WorkspaceId &&
                publish.SourceRange == wholeBank && publish.TargetSpaceId == composition.Plan.OutputSpaceId && publish.TargetRange == bank.OutputRange);
            int previousLocalSequence = seed.Sequence;
            foreach (CompositionOperation parent in bank.LocalComposition.Plan.OrderedOperations)
            {
                CompositionOperation actual = Take(bank.BankId + "/" + parent.OperationId);
                Require(actual.Kind == parent.Kind && actual.TargetSpaceId == bank.WorkspaceId && actual.TargetRange == parent.TargetRange &&
                    actual.Sequence > previousLocalSequence && actual.Sequence < publish.Sequence &&
                    actual.SourceSpaceId == Bind(parent.SourceSpaceId) && actual.SourceRange == parent.SourceRange &&
                    actual.PatchBytes.Span.SequenceEqual(parent.PatchBytes.Span) && actual.OverlapPolicy == OverlapPolicy.ReplaceExisting);
                previousLocalSequence = actual.Sequence;
                if (parent.ExternalProcessorInvocation is { } expected)
                {
                    ExternalProcessorInvocation invocation = actual.ExternalProcessorInvocation!;
                    Require(expected.ProcessorId == invocation.ProcessorId && expected.ToolBindingId == invocation.ToolBindingId &&
                        expected.AllowedReadRanges.SequenceEqual(invocation.AllowedReadRanges) && expected.AllowedWriteRanges.SequenceEqual(invocation.AllowedWriteRanges) &&
                        expected.AllowedWriteRangeSections.SequenceEqual(invocation.AllowedWriteRangeSections) &&
                        expected.OutputAssertions.SequenceEqual(invocation.OutputAssertions) && ReferenceEquals(expected.ProtocolPlan, invocation.ProtocolPlan) &&
                        expected.StagedSourceBindings.Select(binding => (Bind(binding.SourceSpaceId), binding.SourceRange, binding.FirmwareRange))
                            .SequenceEqual(invocation.StagedSourceBindings.Select(static binding => ((string?)binding.SourceSpaceId, binding.SourceRange, binding.FirmwareRange))) &&
                        expected.StagedArtifactBindings.Select(binding => (binding.ArtifactId, Bind(binding.SourceSpaceId), binding.SourceRange))
                            .SequenceEqual(invocation.StagedArtifactBindings.Select(static binding => (binding.ArtifactId, (string?)binding.SourceSpaceId, binding.SourceRange))));
                }
            }

            if (bank.BankId == "b-bank")
            {
                if (partial)
                {
                    foreach (ByteRange field in headerAddressFields)
                    {
                        CompositionOperation normalize = Take($"b-bank/normalize/header-0x{field.Start:X}");
                        Require(normalize.Kind == CompositionOperationKind.TransformScalar &&
                            normalize.SourceSpaceId == bank.WorkspaceId && normalize.TargetSpaceId == bank.WorkspaceId &&
                            normalize.SourceRange == field &&
                            normalize.TargetRange == normalize.SourceRange &&
                            normalize.ScalarTransform!.Addend == -context.Definition.BankCapacityBytes &&
                            normalize.ScalarTransform.ExpectedBefore is not null &&
                            normalize.Sequence > seed.Sequence && normalize.Sequence < firstProcessor);
                    }
                }
                foreach (CompositionOperation canonical in relocations)
                {
                    CompositionOperation normalize = Take(bank.BankId + "/normalize/" + canonical.OperationId);
                    CompositionOperation restore = Take(bank.BankId + "/restore/" + canonical.OperationId);
                    foreach (CompositionOperation actual in new[] { normalize, restore })
                    {
                        Require(actual.Kind == CompositionOperationKind.TransformScalar && actual.SourceSpaceId == bank.WorkspaceId &&
                            actual.TargetSpaceId == bank.WorkspaceId && actual.SourceRange == canonical.SourceRange && actual.TargetRange == canonical.TargetRange &&
                            actual.ScalarTransform!.Width == canonical.ScalarTransform!.Width && actual.ScalarTransform.ByteOrder == canonical.ScalarTransform.ByteOrder &&
                            actual.ScalarTransform.OverflowPolicy == canonical.ScalarTransform.OverflowPolicy);
                    }

                    Require(normalize.Sequence > seed.Sequence && normalize.Sequence < firstProcessor && restore.Sequence < publish.Sequence &&
                        restore.Sequence > operations[bank.BankId + "/" + bank.LocalComposition.Plan.OrderedOperations[^1].OperationId].Sequence &&
                        normalize.ScalarTransform!.Addend == -canonical.ScalarTransform!.Addend && restore.ScalarTransform!.Addend == canonical.ScalarTransform.Addend &&
                        normalize.ScalarTransform.ExpectedBefore is not null && restore.ScalarTransform.ExpectedBefore is not null &&
                        normalize.ScalarTransform.AddendSource.Kind == canonical.ScalarTransform.AddendSource.Kind &&
                        restore.ScalarTransform.AddendSource == canonical.ScalarTransform.AddendSource &&
                        normalize.ScalarTransform.AddendSource.SourceRegionInstanceId == canonical.ScalarTransform.AddendSource.TargetRegionInstanceId &&
                        normalize.ScalarTransform.AddendSource.TargetRegionInstanceId == canonical.ScalarTransform.AddendSource.SourceRegionInstanceId &&
                        normalize.ScalarTransform.ExpectedBefore.Value + normalize.ScalarTransform.Addend == restore.ScalarTransform.ExpectedBefore.Value);
                }
            }

            string? Bind(string? space)
            {
                return space == bank.LocalComposition.Plan.OutputInitialization.ReferenceSpaceId ? bank.Reference.ArtifactId
                    : space == bank.LocalComposition.Plan.OutputSpaceId ? bank.WorkspaceId : space;
            }
        }

        if (partial && context.Banks.Any(static bank => bank.BankId == "b-bank"))
        {
            CompositionOperation canonical = abFinalizer!;
            CompositionOperation finalizer = Take("ab-replace/b-finalize");
            ExternalProcessorInvocation expected = canonical.ExternalProcessorInvocation!;
            ExternalProcessorInvocation actual = finalizer.ExternalProcessorInvocation!;
            Require(finalizer.Kind == CompositionOperationKind.RunExternalProcessor &&
                finalizer.TargetSpaceId == composition.Plan.OutputSpaceId &&
                finalizer.TargetRange == canonical.TargetRange &&
                finalizer.Sequence > operations["b-bank/publish"].Sequence &&
                expected.ProcessorId == actual.ProcessorId &&
                expected.ToolBindingId == actual.ToolBindingId &&
                expected.AllowedReadRanges.SequenceEqual(actual.AllowedReadRanges) &&
                expected.AllowedWriteRanges.SequenceEqual(actual.AllowedWriteRanges) &&
                expected.AllowedWriteRangeSections.SequenceEqual(actual.AllowedWriteRangeSections) &&
                expected.OutputAssertions.SequenceEqual(actual.OutputAssertions) &&
                ReferenceEquals(expected.ProtocolPlan, actual.ProtocolPlan) &&
                expected.StagedSourceBindings.Select(binding => (composition.Plan.OutputSpaceId,
                        binding.SourceRange, binding.FirmwareRange))
                    .SequenceEqual(actual.StagedSourceBindings.Select(static binding =>
                        (binding.SourceSpaceId, binding.SourceRange, binding.FirmwareRange))) &&
                expected.StagedArtifactBindings.Select(binding => (binding.ArtifactId,
                        composition.Plan.OutputSpaceId, binding.SourceRange))
                    .SequenceEqual(actual.StagedArtifactBindings.Select(static binding =>
                        (binding.ArtifactId, binding.SourceSpaceId, binding.SourceRange))));
        }

        Require(expectedIds.Count == operations.Count);
        return context;

        CompositionOperation Take(string id)
        {
            Require(operations.ContainsKey(id) && expectedIds.Add(id));
            return operations[id];
        }

        static void Require(bool condition)
        {
            if (!condition)
            {
                throw new ArgumentException("AB Replace operations no longer match the checked parent/bank assembly.");
            }
        }
    }

    private sealed record BankProof(string BankId, CompiledComposition Parent, RuntimeReferenceCompilationProof Proof);
}
