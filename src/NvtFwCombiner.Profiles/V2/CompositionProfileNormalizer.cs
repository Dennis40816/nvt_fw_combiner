using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Normalizes schema-validated composition-profile-v2 DTO values without compiling a plan.</summary>
internal static partial class CompositionProfileNormalizer
{
    internal static CompiledProfilePromotion NormalizePromotion(
        CompositionProfilePromotionDocument document,
        string path = "promotion")
    {
        IReadOnlyList<CompositionProfilePromotionBlockerDocument> blockerDocuments = document.Blockers;
        var blockers = new CompiledProfilePromotionBlocker[blockerDocuments.Count];
        for (int index = 0; index < blockerDocuments.Count; index++)
        {
            CompositionProfilePromotionBlockerDocument blocker = blockerDocuments[index];
            string blockerPath = $"{path}.blockers[{index}]";
            blockers[index] = Wrap(blockerPath, () => new CompiledProfilePromotionBlocker(
                blocker.BlockerId,
                NormalizeBlockerKind(blocker.Kind, $"{blockerPath}.kind"),
                blocker.Reason,
                blocker.EvidenceRefs));
        }

        return Wrap(path, () => new CompiledProfilePromotion(
            NormalizePromotionStage(document.Stage, $"{path}.stage"),
            blockers));
    }

    internal static (string ExperienceId, LayoutPolicy LayoutPolicy, InputPolicy InputPolicy) NormalizeExperience(
        CompositionProfileExperienceDocument document,
        string path = "experience")
    {
        LayoutPolicy layoutPolicy = NormalizeLayoutPolicy(document.LayoutPolicy, $"{path}.layoutPolicy");
        InputPolicy inputPolicy = NormalizeInputPolicy(document.InputPolicy, $"{path}.inputPolicy");
        return Wrap(path, () =>
        {
            string experienceId = CanonicalPolicyValueRules.RequireCanonicalId(
                document.ExperienceId,
                nameof(document.ExperienceId));
            return (experienceId, layoutPolicy, inputPolicy);
        });
    }

    internal static CompositionProfileMapBinding NormalizeMapBinding(
        CompositionProfileMapBindingDocument document,
        string path = "mapBinding")
    {
        return Wrap(path, () => new CompositionProfileMapBinding(
            document.FamilyId,
            document.FamilyVersion,
            document.FamilyContentHash,
            document.MapIds,
            document.RequiredRegionIds,
            document.RequiredMetadataStructureIds,
            document.RequiredCapabilityIds,
            document.OptionalRegionIds ?? []));
    }

    internal static InputSelectionGroupDefinition NormalizeInputSelectionGroup(
        CompositionProfileInputSelectionGroupDocument document,
        string path = "inputSelectionGroups[0]")
    {
        return Wrap(path, () => new InputSelectionGroupDefinition(
            document.GroupId,
            document.MemberSlotIds,
            document.MinimumSelected,
            document.MaximumSelected));
    }

    private static CompiledProfilePromotionStage NormalizePromotionStage(string value, string path)
    {
        return value switch
        {
            "known" => CompiledProfilePromotionStage.Known,
            "map-resolvable" => CompiledProfilePromotionStage.MapResolvable,
            "inspectable" => CompiledProfilePromotionStage.Inspectable,
            "authorable" => CompiledProfilePromotionStage.Authorable,
            "compilable" => CompiledProfilePromotionStage.Compilable,
            "executable-candidate" => CompiledProfilePromotionStage.ExecutableCandidate,
            "supported" => CompiledProfilePromotionStage.Supported,
            _ => throw Error(path, "Unknown profile promotion stage."),
        };
    }

    private static CompiledProfilePromotionBlockerKind NormalizeBlockerKind(string value, string path)
    {
        return value switch
        {
            "map" => CompiledProfilePromotionBlockerKind.Map,
            "metadata" => CompiledProfilePromotionBlockerKind.Metadata,
            "operation" => CompiledProfilePromotionBlockerKind.Operation,
            "processor" => CompiledProfilePromotionBlockerKind.Processor,
            "integrity" => CompiledProfilePromotionBlockerKind.Integrity,
            "golden" => CompiledProfilePromotionBlockerKind.Golden,
            "human-review" => CompiledProfilePromotionBlockerKind.HumanReview,
            "ui" => CompiledProfilePromotionBlockerKind.Ui,
            "release" => CompiledProfilePromotionBlockerKind.Release,
            _ => throw Error(path, "Unknown promotion blocker kind."),
        };
    }

    private static LayoutPolicy NormalizeLayoutPolicy(string value, string path)
    {
        return value switch
        {
            "fixed" => LayoutPolicy.Fixed,
            "constrained" => LayoutPolicy.Constrained,
            "user-defined" => LayoutPolicy.UserDefined,
            _ => throw Error(path, "Unknown experience layout policy."),
        };
    }

    private static InputPolicy NormalizeInputPolicy(string value, string path)
    {
        return value switch
        {
            "fixed" => InputPolicy.Fixed,
            "extensible" => InputPolicy.Extensible,
            _ => throw Error(path, "Unknown experience input policy."),
        };
    }

    private static T Wrap<T>(string path, Func<T> factory)
    {
        try
        {
            return factory();
        }
        catch (CompositionProfileNormalizationException)
        {
            throw;
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
        catch (OverflowException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static CompositionProfileNormalizationException Error(
        string path,
        string message,
        Exception? innerException = null)
    {
        return innerException is null
            ? new CompositionProfileNormalizationException(path, message)
            : new CompositionProfileNormalizationException(path, message, innerException);
    }
}
