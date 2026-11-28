// Copyright (c) 2026 Dennis Liu. All rights reserved.
namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Pure viewport geometry for pin collisions; source ranges and row identities stay unchanged.</summary>
internal static class OutputLayoutPanelPinLayout
{
    internal sealed record Candidate(int Index, double Center, double Width);
    internal sealed record Cluster(IReadOnlyList<int> Indices, double Left, double Width);

    internal static IReadOnlyList<Cluster> Arrange(IReadOnlyList<Candidate> candidates, double viewportWidth,
        Func<IReadOnlyList<int>, double> clusterWidth)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(clusterWidth);
        if (viewportWidth <= 0 || candidates.Count == 0) { return []; }
        List<Cluster> clusters = [];
        List<Candidate> current = [];
        double right = double.NegativeInfinity;
        foreach (Candidate candidate in candidates.OrderBy(static item => item.Center).ThenBy(static item => item.Index))
        {
            double left = Math.Clamp(candidate.Center - (candidate.Width / 2), 0, Math.Max(0, viewportWidth - candidate.Width));
            if (current.Count > 0 && left >= right)
            {
                clusters.Add(Place(current, viewportWidth, clusterWidth));
                current.Clear();
            }
            current.Add(candidate);
            right = Math.Max(right, left + candidate.Width);
        }
        if (current.Count > 0) { clusters.Add(Place(current, viewportWidth, clusterWidth)); }
        // Wider cluster labels can collide after merging their original pins. Repeat until every cluster is reachable.
        while (true)
        {
            int collision = -1;
            for (int index = 1; index < clusters.Count; index++)
            {
                if (clusters[index].Left < clusters[index - 1].Left + clusters[index - 1].Width)
                {
                    collision = index;
                    break;
                }
            }
            if (collision < 0) { return clusters.AsReadOnly(); }
            Cluster first = clusters[collision - 1];
            Cluster second = clusters[collision];
            Candidate[] members = [.. candidates.Where(candidate => first.Indices.Contains(candidate.Index) ||
                second.Indices.Contains(candidate.Index))];
            clusters[collision - 1] = Place(members, viewportWidth, clusterWidth);
            clusters.RemoveAt(collision);
        }
    }

    private static Cluster Place(IReadOnlyList<Candidate> members, double viewportWidth,
        Func<IReadOnlyList<int>, double> clusterWidth)
    {
        int[] indices = [.. members.Select(static member => member.Index).Order()];
        double width = Math.Min(viewportWidth, Math.Max(18, clusterWidth(indices)));
        double center = members.Average(static member => member.Center);
        double left = Math.Clamp(center - (width / 2), 0, Math.Max(0, viewportWidth - width));
        return new(indices, left, width);
    }
}
