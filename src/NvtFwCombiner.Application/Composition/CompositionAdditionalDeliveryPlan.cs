using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Application plan for one caller-selected artifact declared by the compiled composition.</summary>
public sealed class CompositionAdditionalDeliveryPlan
{
    internal CompositionAdditionalDeliveryPlan(
        string profileId,
        string deliveryKind,
        ByteRange sourceRange,
        string suggestedFileName)
    {
        ProfileId = profileId;
        DeliveryKind = deliveryKind;
        SourceRange = sourceRange;
        SuggestedFileName = suggestedFileName;
    }

    /// <summary>Compiled profile that owns this delivery.</summary>
    public string ProfileId { get; }

    /// <summary>Stable compiled delivery role.</summary>
    public string DeliveryKind { get; }

    /// <summary>Exact half-open primary-output range delivered by Build.</summary>
    public ByteRange SourceRange { get; }

    /// <summary>Plain filename rendered from the accepted primary-output naming tokens.</summary>
    public string SuggestedFileName { get; }
}

/// <summary>One accepted automatic primary name and every compiled optional delivery suggestion.</summary>
public sealed record CompositionOutputPreparation(
    CompositionOutputNamePreview OutputName,
    IReadOnlyList<CompositionAdditionalDeliveryPlan> AdditionalDeliveries);

/// <summary>Renders additional-delivery filenames only from a compiled declaration and accepted naming provenance.</summary>
public static class CompositionAdditionalDeliveryPlanner
{
    /// <summary>Creates the requested compiled delivery, or null when the profile does not declare it.</summary>
    public static CompositionAdditionalDeliveryPlan? TryCreate(
        CompiledComposition composition,
        OutputNamingSummary? outputNaming,
        string deliveryKind)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentException.ThrowIfNullOrWhiteSpace(deliveryKind);
        CompiledAdditionalDelivery? delivery = composition.V2Details.AdditionalDeliveries
            .SingleOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Kind, deliveryKind));
        if (delivery is null)
        {
            return null;
        }

        string fileName = RenderFileName(delivery, outputNaming);
        return new CompositionAdditionalDeliveryPlan(
            composition.V2Details.ProfileId,
            delivery.Kind,
            delivery.SourceRange,
            fileName);
    }

    private static string RenderFileName(
        CompiledAdditionalDelivery delivery,
        OutputNamingSummary? outputNaming)
    {
        if (outputNaming is null)
        {
            throw new InvalidOperationException(
                $"Compiled delivery '{delivery.Kind}' requires accepted output-naming provenance.");
        }

        var tokens = outputNaming.Tokens.ToDictionary(
            static token => token.TokenId,
            static token => token.Value,
            StringComparer.Ordinal);
        string fileName = delivery.FileNameTemplate;
        foreach (string tokenId in delivery.RequiredTokenIds)
        {
            if (!tokens.TryGetValue(tokenId, out string? tokenValue) ||
                string.IsNullOrWhiteSpace(tokenValue))
            {
                throw new InvalidOperationException(
                    $"Compiled delivery '{delivery.Kind}' requires output-naming token '{tokenId}'.");
            }

            fileName = fileName.Replace($"{{{tokenId}}}", tokenValue, StringComparison.Ordinal);
        }

        return fileName.Contains('{') ||
            fileName.Contains('}') ||
            Path.GetFileName(fileName) != fileName ||
            fileName.IndexOfAny(['/', '\\', ':']) >= 0
                ? throw new InvalidOperationException(
                    $"Compiled delivery '{delivery.Kind}' did not render one plain filename.")
                : fileName;
    }
}
