using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

internal static partial class FirmwareFamilyResolutionNormalizer
{
    private sealed record MapInput(int Index, FirmwareImageMapDocument Document)
    {
        internal string Path => $"imageMaps[{Index}]";
    }

    private sealed record AliasDeclaration(
        int Index,
        string Path,
        FirmwareFactAliasDocument Document,
        FirmwareMapFactKey TargetKey,
        FirmwareMapFactKey SourceKey);

    private sealed record ResolvedFact<TFact>(
        FirmwareMapFactKey EffectiveKey,
        FirmwareMapFactKey DirectSourceKey,
        TFact Value,
        IReadOnlyList<AliasDeclaration> AliasChain)
        where TFact : class, IFirmwareMapFact;

    private sealed class FactAliasResolver<TFact>
        where TFact : class, IFirmwareMapFact
    {
        private readonly IReadOnlyDictionary<FirmwareMapFactKey, TFact> _direct;
        private readonly IReadOnlyDictionary<FirmwareMapFactKey, AliasDeclaration> _aliases;
        private readonly Dictionary<FirmwareMapFactKey, ResolvedFact<TFact>> _resolved = [];

        internal FactAliasResolver(
            IReadOnlyDictionary<FirmwareMapFactKey, TFact> direct,
            IEnumerable<AliasDeclaration> aliases)
        {
            ArgumentNullException.ThrowIfNull(direct);
            ArgumentNullException.ThrowIfNull(aliases);
            _direct = direct;
            _aliases = aliases.ToDictionary(static alias => alias.TargetKey);
            foreach (FirmwareMapFactKey target in _aliases.Keys)
            {
                if (_direct.ContainsKey(target))
                {
                    AliasDeclaration alias = _aliases[target];
                    throw Error(alias.Path, $"Alias target '{DescribeKey(target)}' also has a direct provider.");
                }
            }
        }

        internal Dictionary<FirmwareMapFactKey, ResolvedFact<TFact>> ResolveAll(
            IEnumerable<FirmwareMapFactKey> expected)
        {
            ArgumentNullException.ThrowIfNull(expected);
            FirmwareMapFactKey[] required = [.. expected];
            foreach (FirmwareMapFactKey key in required)
            {
                RequireProvider(key, "imageMaps");
            }

            foreach (AliasDeclaration alias in _aliases.Values)
            {
                RequireProvider(alias.SourceKey, $"{alias.Path}.source");
            }

            FirmwareMapFactKey[] ordered = AcyclicDependencyGraph.Sort(
                required.Concat(_aliases.Keys),
                key => _aliases.TryGetValue(key, out AliasDeclaration? alias)
                    ? [alias.SourceKey]
                    : [],
                (sourceKey, cycleKey) => Error(
                    $"{_aliases[sourceKey].Path}.source",
                    $"Fact alias cycle includes '{DescribeKey(cycleKey)}'."));
            foreach (FirmwareMapFactKey key in ordered)
            {
                if (_direct.TryGetValue(key, out TFact? value))
                {
                    _resolved.Add(key, new ResolvedFact<TFact>(key, key, value, []));
                    continue;
                }

                AliasDeclaration alias = _aliases[key];
                ResolvedFact<TFact> source = _resolved[alias.SourceKey];
                _resolved.Add(key, new ResolvedFact<TFact>(
                    key,
                    source.DirectSourceKey,
                    source.Value,
                    [alias, .. source.AliasChain]));
            }

            return _resolved;
        }

        private void RequireProvider(FirmwareMapFactKey key, string path)
        {
            if (!_direct.ContainsKey(key) && !_aliases.ContainsKey(key))
            {
                throw Error(path, $"Fact '{DescribeKey(key)}' has no direct provider or alias.");
            }
        }
    }
}
