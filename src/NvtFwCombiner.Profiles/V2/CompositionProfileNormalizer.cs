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
        ArgumentNullException.ThrowIfNull(document);
        IReadOnlyList<CompositionProfilePromotionBlockerDocument> blockerDocuments = RequireList(
            document.Blockers,
            $"{path}.blockers");
        var blockers = new CompiledProfilePromotionBlocker[blockerDocuments.Count];
        for (int index = 0; index < blockerDocuments.Count; index++)
        {
            CompositionProfilePromotionBlockerDocument blocker = blockerDocuments[index] ?? throw Error(
                $"{path}.blockers[{index}]",
                "Promotion blocker cannot be null.");
            string blockerPath = $"{path}.blockers[{index}]";
            blockers[index] = Wrap(blockerPath, () => new CompiledProfilePromotionBlocker(
                blocker.BlockerId,
                NormalizeBlockerKind(blocker.Kind, $"{blockerPath}.kind"),
                blocker.Reason,
                RequireList(blocker.EvidenceRefs, $"{blockerPath}.evidenceRefs")));
        }

        return Wrap(path, () => new CompiledProfilePromotion(
            NormalizePromotionStage(document.Stage, $"{path}.stage"),
            blockers));
    }

    internal static CompositionProfileExperience NormalizeExperience(
        CompositionProfileExperienceDocument document,
        string path = "experience")
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateAudience(document.Audience, $"{path}.audience");
        LayoutPolicy layoutPolicy = NormalizeLayoutPolicy(document.LayoutPolicy, $"{path}.layoutPolicy");
        InputPolicy inputPolicy = NormalizeInputPolicy(document.InputPolicy, $"{path}.inputPolicy");
        ValidateTopologyAuthoring(document.TopologyAuthoring, $"{path}.topologyAuthoring");
        return Wrap(path, () => new CompositionProfileExperience(
            document.ExperienceId,
            layoutPolicy,
            inputPolicy,
            document.DisplayNameKey));
    }

    internal static CompositionProfileMapBinding NormalizeMapBinding(
        CompositionProfileMapBindingDocument document,
        string path = "mapBinding")
    {
        ArgumentNullException.ThrowIfNull(document);
        return Wrap(path, () => new CompositionProfileMapBinding(
            document.FamilyId,
            document.FamilyVersion,
            document.FamilyContentHash,
            RequireList(document.MapIds, $"{path}.mapIds"),
            RequireList(document.RequiredRegionIds, $"{path}.requiredRegionIds"),
            RequireList(
                document.RequiredMetadataStructureIds,
                $"{path}.requiredMetadataStructureIds"),
            RequireList(document.RequiredCapabilityIds, $"{path}.requiredCapabilityIds"),
            document.OptionalRegionIds ?? []));
    }

    internal static CompositionProfileInputSelectionGroup NormalizeInputSelectionGroup(
        CompositionProfileInputSelectionGroupDocument document,
        string path = "inputSelectionGroups[0]")
    {
        ArgumentNullException.ThrowIfNull(document);
        return Wrap(path, () => new CompositionProfileInputSelectionGroup(
            document.GroupId,
            RequireList(document.MemberSlotIds, $"{path}.memberSlotIds"),
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

    private static void ValidateAudience(string value, string path)
    {
        if (value is not ("system" or "dp" or "ctrlram" or "advanced"))
        {
            throw Error(path, "Unknown experience audience.");
        }
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

    private static void ValidateTopologyAuthoring(string value, string path)
    {
        if (value is not ("hidden" or "single-or-cascade" or "exact-count"))
        {
            throw Error(path, "Unknown topology authoring policy.");
        }
    }

    private static IReadOnlyList<T> RequireList<T>(IReadOnlyList<T>? values, string path)
    {
        return values ?? throw Error(path, "Required array is missing.");
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
