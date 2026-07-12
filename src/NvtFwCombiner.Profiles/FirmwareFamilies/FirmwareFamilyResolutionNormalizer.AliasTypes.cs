using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

public static partial class FirmwareFamilyResolutionNormalizer
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

    private sealed record CapabilityDirect(
        FirmwareCapabilityFact Value,
        FirmwareFactApplicability Applicability);

    private sealed class FactAliasResolver<TFact>
        where TFact : class, IFirmwareMapFact
    {
        private readonly IReadOnlyDictionary<FirmwareMapFactKey, TFact> _direct;
        private readonly IReadOnlyDictionary<FirmwareMapFactKey, AliasDeclaration> _aliases;
        private readonly Dictionary<FirmwareMapFactKey, ResolvedFact<TFact>> _resolved = [];
        private readonly Dictionary<FirmwareMapFactKey, DependencyVisitState> _states = [];

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
            foreach (FirmwareMapFactKey key in expected)
            {
                _ = Resolve(key, "imageMaps");
            }

            foreach (AliasDeclaration alias in _aliases.Values)
            {
                _ = Resolve(alias.TargetKey, alias.Path);
            }

            return _resolved;
        }

        private ResolvedFact<TFact> Resolve(FirmwareMapFactKey key, string path)
        {
            if (_resolved.TryGetValue(key, out ResolvedFact<TFact>? existing))
            {
                return existing;
            }

            var pending = new Stack<(FirmwareMapFactKey Key, string Path)>();
            pending.Push((key, path));
            while (pending.Count != 0)
            {
                (FirmwareMapFactKey currentKey, string currentPath) = pending.Peek();
                if (_resolved.ContainsKey(currentKey))
                {
                    _ = pending.Pop();
                    continue;
                }

                if (_direct.TryGetValue(currentKey, out TFact? value))
                {
                    _states[currentKey] = DependencyVisitState.Resolved;
                    _resolved.Add(currentKey, new ResolvedFact<TFact>(currentKey, currentKey, value, []));
                    _ = pending.Pop();
                    continue;
                }

                if (!_aliases.TryGetValue(currentKey, out AliasDeclaration? alias))
                {
                    throw Error(currentPath, $"Fact '{DescribeKey(currentKey)}' has no direct provider or alias.");
                }

                _states[currentKey] = DependencyVisitState.Visiting;
                FirmwareMapFactKey sourceKey = alias.SourceKey;
                if (_resolved.TryGetValue(sourceKey, out ResolvedFact<TFact>? source))
                {
                    AliasDeclaration[] chain = [alias, .. source.AliasChain];
                    _states[currentKey] = DependencyVisitState.Resolved;
                    _resolved.Add(currentKey, new ResolvedFact<TFact>(
                        currentKey,
                        source.DirectSourceKey,
                        source.Value,
                        chain));
                    _ = pending.Pop();
                    continue;
                }

                if (_states.TryGetValue(sourceKey, out DependencyVisitState state) &&
                    state == DependencyVisitState.Visiting)
                {
                    throw Error($"{alias.Path}.source", $"Fact alias cycle includes '{DescribeKey(sourceKey)}'.");
                }

                pending.Push((sourceKey, $"{alias.Path}.source"));
            }

            return _resolved[key];
        }
    }

    private sealed class DependencyFrame
    {
        internal DependencyFrame(FirmwareMapFactKey key, FirmwareMapFactKey[] dependencies)
        {
            Key = key;
            Dependencies = dependencies;
        }

        internal FirmwareMapFactKey Key { get; }

        internal FirmwareMapFactKey[] Dependencies { get; }

        internal int NextIndex { get; set; }
    }

    private enum DependencyVisitState
    {
        Visiting,
        Resolved,
    }
}
