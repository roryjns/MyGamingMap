using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Hosting; // for IWebHostEnvironment

namespace MyGamingMap.API.Models.DTOs;

public sealed class FeatureMatrix
{
    public required IReadOnlyList<DateTime> Months { get; init; }
    public required IReadOnlyList<string> FeatureNames { get; init; }
    public required double[][] Values { get; init; } // [monthIndex][featureIndex]
}

public static class FeatureMatrixDebugWriter
{
    public static string DebugRoot { get; set; } =
        Path.Combine(Directory.GetCurrentDirectory(), "debug");

    public static void WriteCsv(
        FeatureMatrix matrix,
        string fileName,
        IReadOnlyList<int>? segmentStarts = null,
        bool includeEraColumn = false)
    {
        var path = ResolvePath(fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(path);

        var months = matrix.Months;
        var names = matrix.FeatureNames;
        var values = matrix.Values;

        var nMonths = months.Count;
        var nFeatures = names.Count;

        // Build era index per month if segments provided.
        int[]? eraOfMonth = null;
        if (includeEraColumn && segmentStarts is { Count: > 0 })
        {
            eraOfMonth = new int[nMonths];
            for (var k = 0; k < segmentStarts.Count; k++)
            {
                var start = segmentStarts[k];
                var end = k + 1 < segmentStarts.Count ? segmentStarts[k + 1] : nMonths;
                for (var m = start; m < end; m++) eraOfMonth[m] = k + 1; // 1-based
            }
        }

        using var writer = new StreamWriter(path, append: false, Encoding.UTF8);

        // Header
        if (includeEraColumn)
            writer.Write("Era,");

        writer.Write("Month");

        foreach (var name in names)
        {
            writer.Write(',');
            writer.Write(Escape(name));
        }
        writer.WriteLine();

        // Rows (one per month)
        for (var m = 0; m < nMonths; m++)
        {
            if (includeEraColumn)
            {
                writer.Write(eraOfMonth?[m] ?? 0);
                writer.Write(',');
            }

            writer.Write(months[m].ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            var row = values[m];
            for (var j = 0; j < nFeatures; j++)
            {
                writer.Write(',');
                writer.Write(row[j].ToString("R", CultureInfo.InvariantCulture));
            }
            writer.WriteLine();
        }
    }

    private static string Escape(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? "\"" + s.Replace("\"", "\"\"") + "\""
            : s;

    private static string ResolvePath(string fileName) =>
        Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(DebugRoot, fileName);
}

public sealed class FeatureMatrixBuilder
{
    /// Minimum coverage required for a CategoryShare to contribute features
    /// in a given month. Below this, all labels of that dimension are emitted
    /// as 0 for that month.
    public const double MinCategoryCoverage = 0.4;

    public FeatureMatrix Build(IReadOnlyList<MonthlyAnalytic> months)
    {
        if (months.Count == 0)
        {
            return new FeatureMatrix
            {
                Months = [],
                FeatureNames = [],
                Values = []
            };
        }

        var ordered = months.OrderBy(m => m.Month).ToList();

        // Pass 1: build per-month feature dictionaries.
        var perMonth = ordered.Select(BuildMonthFeatures).ToList();

        // Pass 2: union of feature names in deterministic order.
        var namesSet = new HashSet<string>(StringComparer.Ordinal);

        foreach (var monthFeatures in perMonth)
        {
            foreach (var key in monthFeatures.Keys) namesSet.Add(key);
        }

        var featureNames = OrderFeatureNames(namesSet);

        // Pass 3: project each month onto the fixed column set.
        var values = new double[ordered.Count][];

        for (var i = 0; i < ordered.Count; i++)
        {
            var row = new double[featureNames.Count];
            var monthFeatures = perMonth[i];

            for (var j = 0; j < featureNames.Count; j++)
            {
                row[j] = monthFeatures.TryGetValue(featureNames[j], out var v) ? v : 0;
            }

            values[i] = row;
        }

        return new FeatureMatrix
        {
            Months = ordered.Select(m => m.Month).ToList(),
            FeatureNames = featureNames,
            Values = values
        };
    }

    private static Dictionary<string, double> BuildMonthFeatures(MonthlyAnalytic month)
    {
        var features = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["ActiveGamesCount"] = month.ActiveGamesCount,
            ["GamesStarted"] = month.GamesStarted,
            ["GamesEnded"] = month.GamesEnded,
            ["HoursPlayedEstimate"] = month.HoursPlayedEstimate,
            ["NewGameHoursShare"] = month.NewGameHoursShare,
            ["LongTailHoursShare"] = month.LongTailHoursShare,
            ["TopGameHoursShare"] = month.TopGameHoursShare,
            ["Top3GamesHoursShare"] = month.Top3GamesHoursShare,
            ["HoursConcentration"] = month.HoursConcentration,
            ["AverageTrophyProgress"] = month.AverageTrophyProgress,
            ["PlatinumActiveCount"] = month.PlatinumActiveCount,
            ["PlatinumActiveRatio"] = month.PlatinumActiveRatio
        };

        AddCategoryHours(features, "Platform", month.PlatformShare, month.HoursPlayedEstimate);
        AddCategoryHours(features, "Mode", month.GameModeShare, month.HoursPlayedEstimate);
        AddCategoryHours(features, "Genre", month.GenreShare, month.HoursPlayedEstimate);
        AddCategoryHours(features, "Theme", month.ThemeShare, month.HoursPlayedEstimate);
        AddCategoryHours(features, "Developer", month.DeveloperShare, month.HoursPlayedEstimate);
        AddCategoryHours(features, "Publisher", month.PublisherShare, month.HoursPlayedEstimate);
        AddCategoryHours(features, "Franchise", month.FranchiseShare, month.HoursPlayedEstimate);
        AddCategoryHours(features, "ESRB", month.EsrbRatingShare, month.HoursPlayedEstimate);
        AddCategoryHours(features, "PEGI", month.PegiRatingShare, month.HoursPlayedEstimate);
        AddCategoryHours(features, "ReleaseYear", month.ReleaseYearShare, month.HoursPlayedEstimate);

        return features;
    }

    private static void AddCategoryHours(Dictionary<string, double> features, string prefix,
    CategoryShare? share, double totalHoursForMonth)
    {
        if (share is null) return;
        if (share.Coverage < MinCategoryCoverage) return;
        if (totalHoursForMonth <= 0) return;

        // Reconstruct hours from normalized shares.
        // hours_for_label = share * coverage * totalHours
        var scale = share.Coverage * totalHoursForMonth;

        foreach (var kvp in share.Shares)
        {
            var label = kvp.Key;
            if (string.IsNullOrWhiteSpace(label)) continue;

            var hours = kvp.Value * scale;
            features[$"{prefix}:{label}"] = hours;
        }
    }

    private static readonly string[] ScalarOrder =
    [
        "ActiveGamesCount",
        "GamesStarted",
        "GamesEnded",
        "HoursPlayedEstimate",
        "NewGameHoursShare",
        "LongTailHoursShare",
        "TopGameHoursShare",
        "Top3GamesHoursShare",
        "HoursConcentration",
        "AverageTrophyProgress",
        "PlatinumActiveCount",
        "PlatinumActiveRatio",
    ];

    private static readonly string[] DimensionOrder =
    [
        "Platform",
        "Mode",
        "GameType",
        "Genre",
        "Theme",
        "Developer",
        "Publisher",
        "Franchise",
        "ESRB",
        "PEGI",
        "ReleaseYear"
    ];

    private static List<string> OrderFeatureNames(HashSet<string> allNames)
    {
        var result = new List<string>(allNames.Count);
        var consumed = new HashSet<string>(StringComparer.Ordinal);

        // Scalars in fixed order.
        foreach (var name in ScalarOrder)
        {
            if (allNames.Contains(name))
            {
                result.Add(name);
                consumed.Add(name);
            }
        }

        // Category dimensions: fixed dimension order, alphabetical label order.
        var grouped = allNames
            .Where(n => !consumed.Contains(n))
            .GroupBy(SplitDimension)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        foreach (var dimension in DimensionOrder)
        {
            if (!grouped.TryGetValue(dimension, out var names)) continue;

            foreach (var name in names.OrderBy(n => SplitLabel(n), StringComparer.Ordinal)) result.Add(name);
        }

        // Any features that do not fit a known dimension (defensive).
        foreach (var name in allNames
            .Where(n => !consumed.Contains(n) && !IsKnownDimension(n))
            .OrderBy(n => n, StringComparer.Ordinal))
        {
            result.Add(name);
        }

        return result;
    }

    private static string SplitDimension(string featureName)
    {
        var idx = featureName.IndexOf(':');
        return idx < 0 ? string.Empty : featureName[..idx];
    }

    private static string SplitLabel(string featureName)
    {
        var idx = featureName.IndexOf(':');
        return idx < 0 ? featureName : featureName[(idx + 1)..];
    }

    private static bool IsKnownDimension(string featureName)
    {
        var dim = SplitDimension(featureName);
        return DimensionOrder.Contains(dim, StringComparer.Ordinal);
    }
}

public sealed class PruningReport
{
    public required IReadOnlyList<string> RetainedFeatureNames { get; init; }
    public required IReadOnlyList<DroppedFeature> DroppedFeatures { get; init; }
    public required int OriginalFeatureCount { get; init; }
    public required int RetainedFeatureCount { get; init; }

    public void WriteTo(TextWriter writer)
    {
        writer.WriteLine($"Original features: {OriginalFeatureCount}");
        writer.WriteLine($"Retained features: {RetainedFeatureCount}");
        writer.WriteLine($"Dropped features:  {DroppedFeatures.Count}");
        writer.WriteLine();

        WriteRetainedByDimension(writer);
        //WriteDroppedByDimension(writer);
    }

    private void WriteRetainedByDimension(TextWriter writer)
    {
        writer.WriteLine("Retained features by dimension:");

        var grouped = RetainedFeatureNames
            .GroupBy(SplitDimension, StringComparer.Ordinal)
            .OrderBy(g => DimensionSortKey(g.Key))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var group in grouped)
        {
            writer.WriteLine($"  {group.Key} ({group.Count()}):");

            var ordered = group.Key == "(scalar/derived)"
                ? group.ToList()                                  // fixed order from builder
                : group.OrderBy(SplitLabel, StringComparer.Ordinal).ToList();

            foreach (var name in ordered)
                writer.WriteLine($"    {name}");
        }

        writer.WriteLine();
    }

    private static string SplitDimension(string featureName)
    {
        var idx = featureName.IndexOf(':');
        return idx < 0 ? "(scalar/derived)" : featureName[..idx];
    }

    private static string SplitLabel(string featureName)
    {
        var idx = featureName.IndexOf(':');
        return idx < 0 ? featureName : featureName[(idx + 1)..];
    }

    private static int DimensionSortKey(string dimension)
    {
        // Same order used by FeatureMatrixBuilder for readability.
        return dimension switch
        {
            "(scalar/derived)" => 0,
            "Platform" => 1,
            "Mode" => 2,
            "Genre" => 3,
            "Theme" => 4,
            "Developer" => 5,
            "Publisher" => 6,
            "Franchise" => 7,
            "ESRB" => 8,
            "PEGI" => 9,
            "ReleaseYear" => 10,
            _ => 99
        };
    }
}

public sealed class FeaturePruningOptions
{
    /// <summary>
    /// A column whose variance across the timeline is below this value
    /// is considered constant and removed. Applies to raw values, so
    /// keep this small. 1e-6 is safe for hours and shares.
    /// </summary>
    public double MinVariance { get; init; } = 1e-5;

    /// <summary>
    /// Per-dimension caps on the number of columns retained, ordered by
    /// total hours across the timeline. Dimensions not listed here are
    /// not capped. Use int.MaxValue for "no cap".
    /// </summary>
    public IReadOnlyDictionary<string, int> DimensionCaps { get; init; }
        = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Developer"] = 15,
            ["Publisher"] = 10,
            ["Franchise"] = 15,
        };

    /// <summary>
    /// Within each dimension, drop labels whose total hours across the
    /// timeline are below this fraction of the dimension's total hours.
    /// 0 disables the filter. 0.005 removes the bottom half of a
    /// long-tail distribution without touching meaningful labels.
    /// </summary>
    public double MinDimensionHourFraction { get; init; } = 0.005;

    /// <summary>
    /// Scalar and derived features are never pruned by dimension caps
    /// or hour thresholds. They are curated by hand.
    /// </summary>
    public bool PreserveScalarsAndDerived { get; init; } = true;
}

public sealed class DroppedFeature
{
    public required string Name { get; init; }
    public required string Dimension { get; init; }
    public required double TotalHours { get; init; }
    public required string Reason { get; init; }
}

public sealed class FeaturePruner(FeaturePruningOptions? options = null)
{
    private readonly FeaturePruningOptions _options = options ?? new FeaturePruningOptions();

    public FeatureMatrix Prune(FeatureMatrix input, out PruningReport report)
    {
        var dropped = new List<DroppedFeature>();
        var keptIndices = new List<int>();

        var n = input.Values.Length;
        if (n == 0)
        {
            report = new PruningReport
            {
                RetainedFeatureNames = Array.Empty<string>(),
                DroppedFeatures = Array.Empty<DroppedFeature>(),
                OriginalFeatureCount = input.FeatureNames.Count,
                RetainedFeatureCount = 0
            };
            return input;
        }

        // Group feature indices by dimension.
        var indicesByDimension = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var scalarIndices = new List<int>();

        for (var j = 0; j < input.FeatureNames.Count; j++)
        {
            var name = input.FeatureNames[j];
            var colon = name.IndexOf(':');

            if (colon < 0)
            {
                scalarIndices.Add(j);
                continue;
            }

            var dimension = name[..colon];
            if (!indicesByDimension.TryGetValue(dimension, out var list))
            {
                list = [];
                indicesByDimension[dimension] = list;
            }
            list.Add(j);
        }

        // Scalars and derived: always kept (subject to variance filter).
        foreach (var j in scalarIndices)
        {
            var variance = Variance(input.Values, j);
            if (variance < _options.MinVariance)
            {
                dropped.Add(new DroppedFeature
                {
                    Name = input.FeatureNames[j],
                    Dimension = "(scalar)",
                    TotalHours = Total(input.Values, j),
                    Reason = variance == 0 ? "constant" : "low variance"
                });
                continue;
            }

            keptIndices.Add(j);
        }

        // Each dimension: apply variance filter, then hour threshold, then cap.
        foreach (var (dimension, indices) in indicesByDimension)
        {
            var totalHoursByIndex = new Dictionary<int, double>();
            foreach (var j in indices)
                totalHoursByIndex[j] = Total(input.Values, j);

            // Step 1: variance filter.
            var afterVariance = new List<int>();
            foreach (var j in indices)
            {
                var variance = Variance(input.Values, j);
                if (variance < _options.MinVariance)
                {
                    dropped.Add(new DroppedFeature
                    {
                        Name = input.FeatureNames[j],
                        Dimension = dimension,
                        TotalHours = totalHoursByIndex[j],
                        Reason = variance == 0 ? "constant" : "low variance"
                    });
                    continue;
                }

                afterVariance.Add(j);
            }

            // Step 2: hour threshold within the dimension.
            var dimensionTotal = afterVariance.Sum(j => totalHoursByIndex[j]);
            var minHours = dimensionTotal * _options.MinDimensionHourFraction;

            var afterThreshold = new List<int>();
            foreach (var j in afterVariance)
            {
                if (totalHoursByIndex[j] < minHours)
                {
                    dropped.Add(new DroppedFeature
                    {
                        Name = input.FeatureNames[j],
                        Dimension = dimension,
                        TotalHours = totalHoursByIndex[j],
                        Reason = "below hour threshold"
                    });
                    continue;
                }

                afterThreshold.Add(j);
            }

            // Step 3: dimension cap.
            var cap = _options.DimensionCaps.TryGetValue(dimension, out var c)
                ? c
                : int.MaxValue;

            if (afterThreshold.Count <= cap)
            {
                keptIndices.AddRange(afterThreshold);
                continue;
            }

            var sortedByHours = afterThreshold
                .OrderByDescending(j => totalHoursByIndex[j])
                .ToList();

            for (var k = 0; k < sortedByHours.Count; k++)
            {
                var j = sortedByHours[k];

                if (k < cap)
                {
                    keptIndices.Add(j);
                }
                else
                {
                    dropped.Add(new DroppedFeature
                    {
                        Name = input.FeatureNames[j],
                        Dimension = dimension,
                        TotalHours = totalHoursByIndex[j],
                        Reason = "over dimension cap"
                    });
                }
            }
        }

        // Preserve original column order.
        keptIndices.Sort();

        var newNames = keptIndices
            .Select(j => input.FeatureNames[j])
            .ToList();

        var newValues = new double[n][];
        for (var i = 0; i < n; i++)
        {
            var row = new double[keptIndices.Count];
            for (var k = 0; k < keptIndices.Count; k++)
                row[k] = input.Values[i][keptIndices[k]];
            newValues[i] = row;
        }

        var pruned = new FeatureMatrix
        {
            Months = input.Months,
            FeatureNames = newNames,
            Values = newValues
        };

        report = new PruningReport
        {
            RetainedFeatureNames = newNames,
            DroppedFeatures = dropped,
            OriginalFeatureCount = input.FeatureNames.Count,
            RetainedFeatureCount = newNames.Count
        };

        return pruned;
    }

    private static double Total(double[][] values, int columnIndex)
    {
        double sum = 0;
        for (var i = 0; i < values.Length; i++)
            sum += values[i][columnIndex];
        return sum;
    }

    private static double Variance(double[][] values, int columnIndex)
    {
        var n = values.Length;
        if (n == 0) return 0;

        double sum = 0;
        for (var i = 0; i < n; i++)
            sum += values[i][columnIndex];

        var mean = sum / n;

        double sq = 0;
        for (var i = 0; i < n; i++)
        {
            var d = values[i][columnIndex] - mean;
            sq += d * d;
        }

        return sq / n;
    }

    private static List<int> PruneByCorrelation(
    FeatureMatrix input,
    List<int> keptIndices,
    double maxAbsCorr,
    bool orderByMagnitude,
    List<DroppedFeature> dropped)
    {
        int n = input.Values.Length;

        // Precompute mean and std for each candidate.
        var stats = new Dictionary<int, (double Mean, double Std)>();
        foreach (var j in keptIndices)
        {
            double sum = 0;
            for (int i = 0; i < n; i++) sum += input.Values[i][j];
            double mean = sum / n;

            double sq = 0;
            for (int i = 0; i < n; i++)
            {
                double d = input.Values[i][j] - mean;
                sq += d * d;
            }
            stats[j] = (mean, Math.Sqrt(sq / n));
        }

        // Order of consideration. Higher total |value| wins collisions.
        var ordered = keptIndices.AsEnumerable();
        if (orderByMagnitude)
        {
            ordered = ordered.OrderByDescending(j =>
            {
                double s = 0;
                for (int i = 0; i < n; i++) s += Math.Abs(input.Values[i][j]);
                return s;
            });
        }

        var kept = new List<int>();
        foreach (var j in ordered)
        {
            var (meanJ, stdJ) = stats[j];
            if (stdJ < 1e-12)
            {
                dropped.Add(new DroppedFeature
                {
                    Name = input.FeatureNames[j],
                    Dimension = DimensionOf(input.FeatureNames[j]),
                    TotalHours = Total(input.Values, j),
                    Reason = "constant"
                });
                continue;
            }

            bool collides = false;
            foreach (var k in kept)
            {
                var (meanK, stdK) = stats[k];
                if (stdK < 1e-12) continue;

                double corr = 0;
                for (int i = 0; i < n; i++)
                    corr += (input.Values[i][j] - meanJ)
                          * (input.Values[i][k] - meanK);
                corr /= n * stdJ * stdK;

                if (Math.Abs(corr) > maxAbsCorr)
                {
                    collides = true;
                    dropped.Add(new DroppedFeature
                    {
                        Name = input.FeatureNames[j],
                        Dimension = DimensionOf(input.FeatureNames[j]),
                        TotalHours = Total(input.Values, j),
                        Reason = $"corr {corr:F3} w/ {input.FeatureNames[k]}"
                    });
                    break;
                }
            }

            if (!collides) kept.Add(j);
        }

        kept.Sort();
        return kept;
    }

    private static string DimensionOf(string featureName)
    {
        var idx = featureName.IndexOf(':');
        return idx < 0 ? "(scalar)" : featureName[..idx];
    }
}

public sealed class SmoothingOptions
{
    /// <summary>
    /// Rolling window size in months. 1 disables smoothing.
    /// 3 is a good default for monthly data.
    /// </summary>
    public int WindowSize { get; init; } = 3;

    /// <summary>
    /// If true, the window is centered on each month
    /// (uses [i - W/2, i + W/2]). If false, it trails
    /// (uses [i - W + 1, i]). Centered is correct for segmentation.
    /// </summary>
    public bool Centered { get; init; } = true;
}

public sealed class FeatureSmoother(SmoothingOptions? options = null)
{
    private readonly SmoothingOptions _options = options ?? new SmoothingOptions();

    public FeatureMatrix Smooth(FeatureMatrix input)
    {
        var w = _options.WindowSize;

        if (w <= 1)
            return input;

        var n = input.Values.Length;
        var d = input.FeatureNames.Count;

        if (n == 0 || d == 0)
            return input;

        var halfLeft = _options.Centered ? w / 2 : w - 1;
        var halfRight = _options.Centered ? w / 2 : 0;

        var smoothed = new double[n][];

        for (var i = 0; i < n; i++)
        {
            var lo = Math.Max(0, i - halfLeft);
            var hi = Math.Min(n - 1, i + halfRight);
            var count = hi - lo + 1;

            var row = new double[d];

            for (var j = 0; j < d; j++)
            {
                double sum = 0;
                for (var k = lo; k <= hi; k++)
                    sum += input.Values[k][j];

                row[j] = sum / count;
            }

            smoothed[i] = row;
        }

        return new FeatureMatrix
        {
            Months = input.Months,
            FeatureNames = input.FeatureNames,
            Values = smoothed
        };
    }
}

public sealed class FeatureNormalizer
{
    /// Columns whose standard deviation is below this value are treated
    /// as constant. Their normalized values are set to 0.
    /// Such columns should already have been pruned, but this is defensive.
    public const double MinStdDev = 0.01;

    public FeatureMatrix ZScore(FeatureMatrix input)
    {
        var n = input.Values.Length;
        var d = input.FeatureNames.Count;

        if (n == 0 || d == 0)
            return input;

        var means = new double[d];
        var stdDevs = new double[d];

        // First pass: means.
        for (var j = 0; j < d; j++)
        {
            double sum = 0;
            for (var i = 0; i < n; i++)
                sum += input.Values[i][j];

            means[j] = sum / n;
        }

        // Second pass: standard deviations.
        for (var j = 0; j < d; j++)
        {
            double sq = 0;
            for (var i = 0; i < n; i++)
            {
                var diff = input.Values[i][j] - means[j];
                sq += diff * diff;
            }

            stdDevs[j] = Math.Sqrt(sq / n);
        }

        // Third pass: emit normalized values.
        var normalized = new double[n][];

        for (var i = 0; i < n; i++)
        {
            normalized[i] = new double[d];

            for (var j = 0; j < d; j++)
            {
                normalized[i][j] = stdDevs[j] > MinStdDev
                    ? (input.Values[i][j] - means[j]) / stdDevs[j]
                    : 0;
            }
        }

        return new FeatureMatrix
        {
            Months = input.Months,
            FeatureNames = input.FeatureNames,
            Values = normalized
        };
    }
}