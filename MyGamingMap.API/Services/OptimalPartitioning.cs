using MyGamingMap.API.Models.DTOs;

namespace MyGamingMap.API.Services;

public sealed class OptimalPartitioningOptions
{
    public int K { get; init; } = 7;

    public int MinEraMonths { get; init; } = 3;

    /// Per-feature weights applied inside the cost function. If null,
    /// every feature is weighted equally. If provided, must have the
    /// same length as the feature matrix's column count.
    public double[]? FeatureWeights { get; init; }
}

public sealed class OptimalPartitioningSegmenter(OptimalPartitioningOptions? options = null)
{
    private readonly OptimalPartitioningOptions _options = options ?? new OptimalPartitioningOptions();

    /// Returns the start indices (0-based) of each segment. The first
    /// segment always starts at 0, so the returned array has length K
    /// and its first element is 0.
    public (IReadOnlyList<int> segments, double WithinCost) Segment(FeatureMatrix matrix)
    {
        var n = matrix.Values.Length;
        var d = matrix.FeatureNames.Count;
        var k = _options.K;
        var l = _options.MinEraMonths;

        if (n == 0 || d == 0 || k < 2) return ([], 0);

        if (n < k * l)
            throw new InvalidOperationException($"Cannot partition {n} months into {k} segments with min length {l}.");

        // Feature weights: 1.0 for each column by default.
        var weights = _options.FeatureWeights ?? [.. Enumerable.Repeat(1.0, d)];

        if (weights.Length != d)
            throw new InvalidOperationException($"FeatureWeights length {weights.Length} does not match feature count {d}.");

        // Precompute prefix sums and prefix sums of squares per column,
        // weighted, so segment cost is O(d) instead of O(d * length).
        var prefixW = new double[n + 1, d];
        var prefixWX = new double[n + 1, d];
        var prefixWXX = new double[n + 1, d];
        var prefixWeight = new double[n + 1];

        for (var i = 0; i < n; i++)
        {
            prefixWeight[i + 1] = prefixWeight[i];

            for (var j = 0; j < d; j++)
            {
                var w = weights[j];
                var x = matrix.Values[i][j];

                prefixW[i + 1, j] = prefixW[i, j] + w;
                prefixWX[i + 1, j] = prefixWX[i, j] + w * x;
                prefixWXX[i + 1, j] = prefixWXX[i, j] + w * x * x;

                // Accumulate total weight once, on the first column.
                if (j == 0) prefixWeight[i + 1] += w;
            }
        }

        // Cost of segment [start, end). Weighted sum of within-segment
        // variances across all features.
        double SegmentCost(int start, int end)
        {
            var length = end - start;
            if (length <= 0) return 0;

            double cost = 0;

            for (var j = 0; j < d; j++)
            {
                var w = prefixW[end, j] - prefixW[start, j];

                if (w <= 0) continue;

                var wx = prefixWX[end, j] - prefixWX[start, j];
                var wxx = prefixWXX[end, j] - prefixWXX[start, j];

                var mean = wx / w;
                var variance = (wxx / w) - (mean * mean);

                if (variance < 0) variance = 0;

                // Weight by feature weight times segment length,
                // so length affects cost comparably across segments.
                cost += variance * weights[j] * length;
            }

            return cost;
        }

        // DP table: cost[k][i] = min cost to partition months [0, i)
        // into k segments. Boundaries[k][i] = the start index of the
        // last segment in that optimal partition.
        var costTable = new double[k + 1, n + 1];
        var boundaryTable = new int[k + 1, n + 1];

        for (var kk = 0; kk <= k; kk++)
        {
            for (var i = 0; i <= n; i++)
            {
                costTable[kk, i] = double.PositiveInfinity;
                boundaryTable[kk, i] = -1;
            }
        }

        costTable[0, 0] = 0;

        for (var kk = 1; kk <= k; kk++)
        {
            for (var i = kk * l; i <= n - (k - kk) * l; i++)
            {
                // The last segment spans [j, i), where j is chosen so
                // that the first kk-1 segments fit in [0, j) with each
                // at least l months.
                for (var j = (kk - 1) * l; j <= i - l; j++)
                {
                    var prev = costTable[kk - 1, j];

                    if (double.IsPositiveInfinity(prev)) continue;

                    var total = prev + SegmentCost(j, i);

                    if (total < costTable[kk, i])
                    {
                        costTable[kk, i] = total;
                        boundaryTable[kk, i] = j;
                    }
                }
            }
        }

        // Backtrack from (k, n).
        var segments = new int[k];
        var pos = n;

        for (var kk = k; kk >= 1; kk--)
        {
            var j = boundaryTable[kk, pos];
            if (j < 0) throw new InvalidOperationException("DP failed to find a valid partition. Check K and MinEraMonths.");
            segments[kk - 1] = j;
            pos = j;
        }

        return (segments, costTable[k, n]);
    }

    /// Convenience: split the matrix into EraSegment-like boundary months.
    public IReadOnlyList<DateTime> SegmentBoundaries(FeatureMatrix matrix)
    {
        var segments = Segment(matrix);
        return [.. segments.segments.Select(i => matrix.Months[i])];
    }

    public double SilhouetteScore(FeatureMatrix m, IReadOnlyList<int> segStarts)
    {
        int n = m.Values.Length, d = m.FeatureNames.Count;
        if (segStarts.Count < 2) return 0;

        // Map each month to its segment id
        var segId = new int[n];
        for (int s = 0; s < segStarts.Count; s++)
        {
            int a = segStarts[s];
            int b = (s < segStarts.Count - 1) ? segStarts[s + 1] : n;
            for (int i = a; i < b; i++) segId[i] = s;
        }

        // Precompute pairwise Euclidean distances (n ≈ 105 → ~5500 pairs, trivial)
        var dist = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                double sum = 0;
                for (int f = 0; f < d; f++)
                {
                    double diff = m.Values[i][f] - m.Values[j][f];
                    sum += diff * diff;
                }
                double dd = Math.Sqrt(sum);
                dist[i, j] = dd;
                dist[j, i] = dd;
            }
        }

        double total = 0;
        for (int i = 0; i < n; i++)
        {
            // a(i): mean distance to others in same segment
            double aSum = 0; int aCount = 0;
            for (int j = 0; j < n; j++)
            {
                if (j == i || segId[j] != segId[i]) continue;
                aSum += dist[i, j]; aCount++;
            }
            double a = aCount > 0 ? aSum / aCount : 0;

            // b(i): min over other segments of mean distance to that segment
            double b = double.PositiveInfinity;
            for (int s = 0; s < segStarts.Count; s++)
            {
                if (s == segId[i]) continue;
                double sum = 0; int count = 0;
                for (int j = 0; j < n; j++)
                {
                    if (segId[j] != s) continue;
                    sum += dist[i, j]; count++;
                }
                if (count > 0) b = Math.Min(b, sum / count);
            }

            double s_i = (aCount == 0 || double.IsPositiveInfinity(b))
                ? 0
                : (b - a) / Math.Max(a, b);
            total += s_i;
        }

        return total / n;
    }
}

public sealed record FeatureContribution(
    string Feature,
    double DeltaSS,
    double Share);

public sealed record BoundaryExplanation(
    DateTime BoundaryMonth,
    int BoundaryIndex,
    double TotalDeltaSS,
    IReadOnlyList<FeatureContribution> TopFeatures);

public static class BoundaryAttribution
{
    /// For each boundary in the segmentation, explains which features drove it.
    /// The attribution is the per-feature reduction in within-segment SS
    /// achieved by splitting the era at that boundary.
    public static IReadOnlyList<BoundaryExplanation> Explain(
        FeatureMatrix matrix,
        IReadOnlyList<int> segStarts,
        int topN = 10)
    {
        int n = matrix.Values.Length;
        int d = matrix.FeatureNames.Count;
        int K = segStarts.Count;

        // Convert to [start, end) ranges
        var ranges = new (int Start, int End)[K];
        for (int s = 0; s < K; s++)
        {
            int a = segStarts[s];
            int b = (s < K - 1) ? segStarts[s + 1] : n;
            ranges[s] = (a, b);
        }

        // Prefix sums and sum of squares per feature.
        // prefixSS[j][i] = Σ_{k<i} (x_kj)². prefixX[j][i] = Σ_{k<i} x_kj.
        var prefixX = new double[d, n + 1];
        var prefixXX = new double[d, n + 1];

        for (int j = 0; j < d; j++)
        {
            for (int i = 0; i < n; i++)
            {
                double x = matrix.Values[i][j];
                prefixX[j, i + 1] = prefixX[j, i] + x;
                prefixXX[j, i + 1] = prefixXX[j, i] + x * x;
            }
        }

        // SS_j for interval [a, b)
        double FeatureSS(int j, int a, int b)
        {
            int len = b - a;
            if (len <= 1) return 0;
            double sum = prefixX[j, b] - prefixX[j, a];
            double sumSq = prefixXX[j, b] - prefixXX[j, a];
            return sumSq - sum * sum / len;
        }

        var result = new List<BoundaryExplanation>();

        // For each internal boundary (exclude the implicit first one at index 0)
        for (int s = 1; s < K; s++)
        {
            int mid = segStarts[s];
            int eraStart = segStarts[s - 1];
            int eraEnd = (s < K - 1) ? segStarts[s + 1] : n;

            // For each feature, compute:
            //   SS(whole era) - [SS(left half) + SS(right half)]
            var contributions = new (int Feature, double Delta)[d];
            double totalDelta = 0;

            for (int j = 0; j < d; j++)
            {
                double ssWhole = FeatureSS(j, eraStart, eraEnd);
                double ssLeft = FeatureSS(j, eraStart, mid);
                double ssRight = FeatureSS(j, mid, eraEnd);
                double delta = ssWhole - ssLeft - ssRight;
                if (delta < 0) delta = 0;
                contributions[j] = (j, delta);
                totalDelta += delta;
            }

            var ordered = contributions
                .OrderByDescending(c => c.Delta)
                .Take(topN)
                .Select(c => new FeatureContribution(
                    matrix.FeatureNames[c.Feature],
                    c.Delta,
                    totalDelta > 1e-12 ? c.Delta / totalDelta : 0))
                .ToList();

            result.Add(new BoundaryExplanation(
                matrix.Months[mid],
                mid,
                totalDelta,
                ordered));
        }

        return result;
    }

    /// Print a formatted report.
    public static void WriteTo(TextWriter writer,
        FeatureMatrix matrix, IReadOnlyList<int> segStarts, int topN = 10)
    {
        var explanations = Explain(matrix, segStarts, topN);
        int K = segStarts.Count;

        for (int s = 0; s < explanations.Count; s++)
        {
            var e = explanations[s];

            // Era before this boundary
            int prevStart = segStarts[s];
            int prevEnd = e.BoundaryIndex;
            // Era after this boundary
            int nextStart = e.BoundaryIndex;
            int nextEnd = (s + 2 < K) ? segStarts[s + 2] : matrix.Months.Count;

            writer.WriteLine();
            writer.WriteLine($"=== Boundary at {e.BoundaryMonth:MM-yyyy} " +
                             $"(index {e.BoundaryIndex}) ===");
            writer.WriteLine($"    Splits [{matrix.Months[prevStart]:MM-yyyy}–" +
                             $"{matrix.Months[prevEnd - 1]:MM-yyyy}] " +
                             $"from [{matrix.Months[nextStart]:MM-yyyy}–" +
                             $"{matrix.Months[nextEnd - 1]:MM-yyyy}]");
            writer.WriteLine($"    Total ΔSS: {e.TotalDeltaSS:F2}");
            writer.WriteLine();
            writer.WriteLine($"    {"Feature",-50} {"ΔSS",-10} {"Share",-8}");
            writer.WriteLine($"    {new string('-', 70)}");

            foreach (var c in e.TopFeatures)
            {
                var name = c.Feature.Length > 48
                    ? c.Feature[..45] + "..."
                    : c.Feature;

                writer.WriteLine(
                    $"    {name,-50} {c.DeltaSS,-10:F2} {c.Share,-8:P1}");
            }
        }
    }
}