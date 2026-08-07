namespace NvtFwCombiner.Domain;

/// <summary>Canonical non-recursive dependency traversal for immutable definition graphs.</summary>
internal static class AcyclicDependencyGraph
{
    internal static T[] Sort<T>(
        IEnumerable<T> roots,
        Func<T, IEnumerable<T>> dependencies,
        Func<T, T, Exception> cycleException,
        IEqualityComparer<T>? comparer = null)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(cycleException);

        Dictionary<T, bool> resolved = new(comparer);
        List<T> ordered = [];
        foreach (T root in roots)
        {
            Stack<(T Node, bool Exit, T Parent, bool HasParent)> pending = [];
            pending.Push((root, false, default!, false));
            while (pending.TryPop(out (T Node, bool Exit, T Parent, bool HasParent) frame))
            {
                if (frame.Exit)
                {
                    resolved[frame.Node] = true;
                    ordered.Add(frame.Node);
                    continue;
                }

                if (resolved.TryGetValue(frame.Node, out bool isResolved))
                {
                    if (!isResolved)
                    {
                        throw cycleException(
                            frame.HasParent ? frame.Parent : frame.Node,
                            frame.Node);
                    }

                    continue;
                }

                resolved.Add(frame.Node, false);
                pending.Push((frame.Node, true, default!, false));
                T[] next = [.. dependencies(frame.Node)];
                for (int index = next.Length - 1; index >= 0; index--)
                {
                    pending.Push((next[index], false, frame.Node, true));
                }
            }
        }

        return [.. ordered];
    }
}
