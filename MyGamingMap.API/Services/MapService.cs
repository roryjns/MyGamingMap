using MyGamingMap.API.Models.DTOs;
using MyGamingMap.API.Services.EraComponents;

namespace MyGamingMap.API.Services;

public static class MapService
{
    public static Map GetMap(List<EnrichedPlayerGame> enrichedGames, List<EnrichedPlayerGame> distinctGames)
    {
        var monthlyAnalytics = CalculateMonthlyAnalytics(enrichedGames);

        // Build, prune, smooth and normalise the monthly feature matrix
        var builder = new FeatureMatrixBuilder();
        var matrix = builder.Build(monthlyAnalytics);

        var pruner = new FeaturePruner(new FeaturePruningOptions
        {
            // Remove a feature if its variance is below this
            MinVariance = 0.05,

            // Remove a category feature if it makes up less than 1% of the category's total hours
            MinDimensionHourFraction = 0.01,

            // Only keep the top N category features by hours played
            DimensionCaps = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Developer"] = 15,
                ["Publisher"] = 10,
                ["Franchise"] = 15,
            },

        });

        var pruned = pruner.Prune(matrix, out var report);
        //report.WriteTo(Console.Out);

        var smoother = new FeatureSmoother(new SmoothingOptions
        {
            WindowSize = 3,
            Centered = true
        });

        var smoothed = smoother.Smooth(pruned);
        var normalizer = new FeatureNormalizer();
        var normalized = normalizer.ZScore(smoothed);

        // Segment the monthly feature matrix
        var minEraMonths = 4;
        int nMonths = normalized.Months.Count;

        if (nMonths < 6)
        {
            var singleEra = new Map
            {
                Eras =
                [
                    new() {
                        Start = normalized.Months[0],
                        End   = normalized.Months[^1],
                    }
                ]
            };

            return singleEra;
        }

        if (nMonths <= 12) minEraMonths = 3;

        double total = TotalSS(normalized);

        var curve = new List<(int K, double Within, double Between, double CH, IReadOnlyList<int> Segs)>();

        int maxK = Math.Min(9, nMonths / minEraMonths);

        for (int K = 2; K <= maxK; K++)
        {
            var seg = new OptimalPartitioningSegmenter(new OptimalPartitioningOptions
            {
                K = K,
                MinEraMonths = minEraMonths,
            });

            var (segs, within) = seg.Segment(normalized);
            double between = total - within;
            double ch = between / (within / (nMonths - K)) / (K - 1);
            curve.Add((K, within, between, ch, segs));
        }

        Console.WriteLine("K\tWithinSS\t\tBetweenSS\t\tCH");
        for (int i = 0; i < curve.Count; i++)
        {
            var (K, Within, Between, CH, Segs) = curve[i];
            Console.WriteLine($"{K}\t{Within:F4}\t\t{Between:F4}\t\t{CH:F4}\t\t");
        }

        var best = curve.OrderByDescending(c => c.CH).First();

        Console.WriteLine($"\nBest K by CH: {best.K} (CH={best.CH:F4})");

        // Analyze the base segmentation and get per-era shares.
        var baseShares = AnalyzeEras(normalized, best.Segs, enrichedGames);

        IReadOnlyList<int> finalSegs;

        // Split eras with a disproportionate share of CH into additional eras
        // Generally produces a better narrative at the cost of statistical optimality
        if (best.K >= 5)
        {
            finalSegs = best.Segs;
        }
        else
        {
            double threshold = 1.25 / best.K;
            int highShareCount = baseShares.Count(s => s > threshold);
            int kTarget = Math.Min(best.K + highShareCount, 9);

            Console.WriteLine(
                $"Base K = {best.K}, threshold = {threshold:P1}, " +
                $"high-share eras = {highShareCount}, K_target = {kTarget}");

            finalSegs = curve.First(c => c.K == kTarget).Segs;
        }

        //BoundaryAttribution.WriteTo(Console.Out, normalized, finalSegs, topN: 10);
        AnalyzeEras(normalized, finalSegs, enrichedGames);

        // Build the list of era objects from the final segmentation
        var map = BuildMap(normalized, finalSegs);

        // Calculate the analytics for each era based on the games that were played during that era
        var eraAnalytics = CalculateEraAnalytics(map, enrichedGames, distinctGames);

        // Determine map components (quiz questions etc) for each era based on the era analytics
        map = PopulateMap(map, eraAnalytics);

        return map;
    }

    private static List<MonthlyAnalytic> CalculateMonthlyAnalytics(List<EnrichedPlayerGame> enrichedGames)
    {
        var datedGames = enrichedGames
            .Where(g =>
                g.PlayerGame.FirstPlayed.HasValue &&
                g.PlayerGame.LastPlayed.HasValue)
            .ToList();

        if (datedGames.Count == 0) return [];

        var firstMonth = new DateTime(
            datedGames.Min(g => g.PlayerGame.FirstPlayed)!.Value.Year,
            datedGames.Min(g => g.PlayerGame.FirstPlayed)!.Value.Month,
            1);

        var lastMonth = new DateTime(
            datedGames.Max(g => g.PlayerGame.LastPlayed)!.Value.Year,
            datedGames.Max(g => g.PlayerGame.LastPlayed)!.Value.Month,
            1);

        var result = new List<MonthlyAnalytic>();

        for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
        {
            result.Add(CalculateMonth(datedGames, month));
        }

        return result;
    }

    private static MonthlyAnalytic CalculateMonth(List<EnrichedPlayerGame> games, DateTime month)
    {
        var monthEnd = month.AddMonths(1);

        var activeGames = games
            .Where(g =>
                g.PlayerGame.FirstPlayed!.Value < monthEnd &&
                g.PlayerGame.LastPlayed!.Value >= month)
            .ToList();

        var gameHours = activeGames
            .Select(g => (Game: g, Hours: EstimateMonthlyHours(g)))
            .Where(x => x.Hours > 0)
            .ToList();

        var hoursPlayedEstimate = gameHours.Sum(x => x.Hours);

        var gamesStarted = activeGames.Count(g =>
            g.PlayerGame.FirstPlayed!.Value >= month &&
            g.PlayerGame.FirstPlayed!.Value < monthEnd);

        var gamesEnded = activeGames.Count(g =>
            g.PlayerGame.LastPlayed!.Value >= month &&
            g.PlayerGame.LastPlayed!.Value < monthEnd);

        var newGameCutoff = month.AddMonths(-1);
        var longTailCutoff = month.AddMonths(-6);

        var newGameHours = gameHours
            .Where(x => x.Game.PlayerGame.FirstPlayed!.Value >= newGameCutoff)
            .Sum(x => x.Hours);

        var longTailHours = gameHours
            .Where(x => x.Game.PlayerGame.FirstPlayed!.Value < longTailCutoff)
            .Sum(x => x.Hours);

        var newGameHoursShare = hoursPlayedEstimate > 0
            ? newGameHours / hoursPlayedEstimate
            : 0;

        var longTailHoursShare = hoursPlayedEstimate > 0
            ? longTailHours / hoursPlayedEstimate
            : 0;

        var topGameHoursShare = 0.0;
        var top3GamesHoursShare = 0.0;
        var hoursConcentration = 0.0;

        if (hoursPlayedEstimate > 0)
        {
            var sortedDesc = gameHours
                .Select(x => x.Hours)
                .OrderByDescending(h => h)
                .ToList();

            topGameHoursShare = sortedDesc[0] / hoursPlayedEstimate;
            top3GamesHoursShare = sortedDesc.Take(3).Sum() / hoursPlayedEstimate;

            foreach (var (Game, Hours) in gameHours)
            {
                var share = Hours / hoursPlayedEstimate;
                hoursConcentration += share * share;
            }
        }

        var averageTrophyProgress = CalculateAverageTrophyProgress(activeGames);

        var platinumActiveCount = activeGames.Count(g =>
            g.PlayerGame.TrophyData.Any(t => t.EarnedTrophies.Platinum > 0));

        var platinumActiveRatio = activeGames.Count > 0
            ? (double)platinumActiveCount / activeGames.Count
            : 0;

        var platformShare = BuildCategoryShare(gameHours, g => g.PlayerGame.Platform, hoursPlayedEstimate);

        var franchiseShare = BuildCategoryShare(gameHours, g => g.IGDBGame?.Franchises, hoursPlayedEstimate);

        var gameModeShare = BuildCategoryShare(
        gameHours,
        g =>
        {
            var modes = g.IGDBGame?.GameModes;

            if (modes is null || modes.Count == 0) return null;

            var hasSinglePlayer = false;
            var hasMultiplayer = false;

            foreach (var mode in modes)
            {
                if (string.IsNullOrWhiteSpace(mode)) continue;
                if (mode.Equals("Single player", StringComparison.OrdinalIgnoreCase)) hasSinglePlayer = true;
                else hasMultiplayer = true;
            }

            return (hasSinglePlayer, hasMultiplayer) switch
            {
                (true, true) => ["Single player + Multiplayer"],
                (true, false) => ["Single player only"],
                (false, true) => ["Multiplayer only"],
                (false, false) => null
            };
        },
        hoursPlayedEstimate);

        var gameTypeShare = BuildCategoryShare(gameHours, g => g.IGDBGame?.GameType, hoursPlayedEstimate);
        var genreShare = BuildCategoryShare(gameHours, g => g.IGDBGame?.Genres, hoursPlayedEstimate);
        var themeShare = BuildCategoryShare(gameHours, g => g.IGDBGame?.Themes, hoursPlayedEstimate);
        var developerShare = BuildCategoryShare(gameHours, g => g.IGDBGame?.Developers, hoursPlayedEstimate);
        var publisherShare = BuildCategoryShare(gameHours, g => g.IGDBGame?.Publishers, hoursPlayedEstimate);
        var esrbRatingShare = BuildCategoryShare(gameHours, g => g.IGDBGame?.ESRB_Rating, hoursPlayedEstimate);
        var pegiRatingShare = BuildCategoryShare(gameHours, g => g.IGDBGame?.PEGI_Rating?.ToString(), hoursPlayedEstimate);

        var releaseYearShare = BuildCategoryShare(
            gameHours,
            g =>
            {
                var releaseDates = g.IGDBGame?.ReleaseDates;

                if (releaseDates == null || releaseDates.Count == 0) return null;

                var earliest = releaseDates
                    .Where(r => r.Date.HasValue)
                    .OrderBy(r => r.Date!.Value)
                    .FirstOrDefault();

                return earliest?.Date?.Year.ToString();
            },
            hoursPlayedEstimate);

        return new MonthlyAnalytic
        {
            Month = month,
            ActiveGamesCount = activeGames.Count,
            GamesStarted = gamesStarted,
            GamesEnded = gamesEnded,
            HoursPlayedEstimate = hoursPlayedEstimate,
            NewGameHoursShare = newGameHoursShare,
            LongTailHoursShare = longTailHoursShare,
            TopGameHoursShare = topGameHoursShare,
            Top3GamesHoursShare = top3GamesHoursShare,
            HoursConcentration = hoursConcentration,
            AverageTrophyProgress = averageTrophyProgress,
            PlatinumActiveCount = platinumActiveCount,
            PlatinumActiveRatio = platinumActiveRatio,
            PlatformShare = platformShare,
            FranchiseShare = franchiseShare,
            GameModeShare = gameModeShare,
            GenreShare = genreShare,
            ThemeShare = themeShare,
            DeveloperShare = developerShare,
            PublisherShare = publisherShare,
            EsrbRatingShare = esrbRatingShare,
            PegiRatingShare = pegiRatingShare,
            ReleaseYearShare = releaseYearShare
        };
    }

    private static double EstimateMonthlyHours(EnrichedPlayerGame game)
    {
        var first = game.PlayerGame.FirstPlayed!.Value;
        var last = game.PlayerGame.LastPlayed!.Value;

        var firstMonth = new DateTime(first.Year, first.Month, 1);
        var lastMonth = new DateTime(last.Year, last.Month, 1);

        var activeMonthCount =
            ((lastMonth.Year - firstMonth.Year) * 12) +
            lastMonth.Month - firstMonth.Month + 1;

        if (activeMonthCount <= 0) return 0;

        return (game.PlayerGame.PlayHours ?? 0) / activeMonthCount;
    }

    private static double CalculateAverageTrophyProgress(List<EnrichedPlayerGame> activeGames)
    {
        double weightedSum = 0;
        double totalHours = 0;

        foreach (var game in activeGames)
        {
            var allocatedHours = EstimateMonthlyHours(game);
            if (allocatedHours <= 0) continue;

            var progress = GetTrophyProgress(game) / 100.0;

            weightedSum += allocatedHours * progress;
            totalHours += allocatedHours;
        }

        return totalHours > 0 ? weightedSum / totalHours : 0;
    }

    private static double GetTrophyProgress(EnrichedPlayerGame game)
    {
        var sets = game.PlayerGame.TrophyData;

        if (sets == null || sets.Count == 0) return 0;

        var max = 0.0;

        foreach (var set in sets) if (set.Progress > max) max = set.Progress;

        if (max < 0) max = 0;
        if (max > 100) max = 100;

        return max;
    }

    private static CategoryShare BuildCategoryShare(List<(EnrichedPlayerGame Game, double Hours)> gameHours,
    Func<EnrichedPlayerGame, List<string>?> selector, double totalHoursForMonth)
    {
        var categoryHours = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var hoursWithCategory = 0.0;

        foreach (var (Game, Hours) in gameHours)
        {
            var categories = selector(Game);

            if (categories == null || categories.Count == 0) continue;

            var hoursPerCategory = Hours / categories.Count;
            var contributed = false;

            foreach (var category in categories)
            {
                if (string.IsNullOrWhiteSpace(category)) continue;

                contributed = true;
                categoryHours.TryGetValue(category, out var existing);
                categoryHours[category] = existing + hoursPerCategory;
            }

            if (contributed) hoursWithCategory += Hours;
        }

        if (hoursWithCategory <= 0)
        {
            return new CategoryShare
            {
                Shares = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
                Coverage = 0
            };
        }

        var shares = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in categoryHours) shares[kvp.Key] = kvp.Value / hoursWithCategory;

        var coverage = totalHoursForMonth > 0 ? hoursWithCategory / totalHoursForMonth : 0;

        return new CategoryShare
        {
            Shares = shares,
            Coverage = coverage
        };
    }

    private static CategoryShare BuildCategoryShare(List<(EnrichedPlayerGame Game, double Hours)> gameHours,
    Func<EnrichedPlayerGame, string?> selector, double totalHoursForMonth)
    {
        return BuildCategoryShare(
            gameHours,
            g =>
            {
                var value = selector(g);
                return string.IsNullOrWhiteSpace(value) ? null : [value];
            },
            totalHoursForMonth);
    }

    private static double TotalSS(FeatureMatrix m)
    {
        int n = m.Values.Length, d = m.FeatureNames.Count;
        double ss = 0;
        for (int j = 0; j < d; j++)
        {
            double mean = 0;
            for (int i = 0; i < n; i++) mean += m.Values[i][j];
            mean /= n;
            for (int i = 0; i < n; i++)
            {
                double diff = m.Values[i][j] - mean;
                ss += diff * diff;
            }
        }
        return ss;
    }

    private static double[] AnalyzeEras(FeatureMatrix m, IReadOnlyList<int> segStarts, IReadOnlyList<EnrichedPlayerGame> enrichedGames)
    {
        int n = m.Values.Length;
        int d = m.FeatureNames.Count;
        int K = segStarts.Count;

        // Convert segment starts to [startIdx, endIdx) pairs.
        var ranges = new (int Start, int End)[K];
        for (int s = 0; s < K; s++)
        {
            int a = segStarts[s];
            int b = (s < K - 1) ? segStarts[s + 1] : n;
            ranges[s] = (a, b);
        }

        // Per-era within-SS.
        var eraSS = new double[K];
        for (int s = 0; s < K; s++)
        {
            var (a, b) = ranges[s];
            double ss = 0;
            for (int f = 0; f < d; f++)
            {
                double mean = 0;
                for (int i = a; i < b; i++) mean += m.Values[i][f];
                mean /= (b - a);
                for (int i = a; i < b; i++)
                {
                    double diff = m.Values[i][f] - mean;
                    ss += diff * diff;
                }
            }
            eraSS[s] = ss;
        }

        double totalWithin = eraSS.Sum();

        // Compute shares.
        var shares = new double[K];
        for (int s = 0; s < K; s++)
            shares[s] = totalWithin > 1e-9 ? eraSS[s] / totalWithin : 0;

        // Print.
        Console.WriteLine();
        Console.WriteLine("=== Per-era diagnostics ===");
        Console.WriteLine();
        Console.WriteLine($"{"Era",-4} {"Range",-18} {"Mo",-3} {"Games",-5} {"Hours",-10} {"WithinSS",-11} {"Share",-7}");
        Console.WriteLine(new string('-', 72));

        for (int s = 0; s < K; s++)
        {
            var (a, b) = ranges[s];
            var startMonth = m.Months[a];
            var endMonth = m.Months[b - 1];
            int months = b - a;

            var gamesInEra = enrichedGames
                .Where(g =>
                {
                    var fp = g.PlayerGame?.FirstPlayed;
                    if (fp is null) return false;
                    var dt = fp.Value;
                    return dt >= startMonth && dt < endMonth.AddMonths(1);
                })
                .ToList();

            int gameCount = gamesInEra.Count;
            double hours = gamesInEra.Sum(g => g.PlayerGame?.PlayHours ?? 0);

            Console.WriteLine(
                $"{s + 1,-4} " +
                $"{startMonth:MM-yyyy}–{endMonth:MM-yyyy}   " +
                $"{months,-4} " +
                $"{gameCount,-6} " +
                $"{hours,-9:F1} " +
                $"{eraSS[s],-11:F3} " +
                $"{shares[s],-7:P1}");
        }

        Console.WriteLine(new string('-', 72));
        Console.WriteLine($"{"Total",-4} {"",-22} {"",-4} {"",-6} {"",-9} {totalWithin,-11:F3}");
        Console.WriteLine();

        return shares;
    }

    private static Map BuildMap(FeatureMatrix matrix, IReadOnlyList<int> segStarts)
    {
        int n = matrix.Months.Count;
        var eras = new List<Era>(segStarts.Count);

        for (int i = 0; i < segStarts.Count; i++)
        {
            int a = segStarts[i];
            int b = (i < segStarts.Count - 1) ? segStarts[i + 1] : n;
            DateTime start = matrix.Months[a];
            DateTime end = matrix.Months[b - 1].AddMonths(1);

            eras.Add(new Era
            {
                Start = start,
                End = end,
                MonthCount = ((end.Year - start.Year) * 12) + end.Month - start.Month,
            });
        }

        return new Map { Eras = eras };
    }

    private static List<EraAnalytics> CalculateEraAnalytics(Map map, List<EnrichedPlayerGame> enrichedGames, List<EnrichedPlayerGame> distinctGames)
    {
        var eraAnalytics = new List<EraAnalytics>();

        foreach (var era in map.Eras)
        {
            var gamesStartedInEra = enrichedGames
                .Where(g =>
                    g.PlayerGame.FirstPlayed.HasValue &&
                    g.PlayerGame.FirstPlayed.Value >= era.Start &&
                    g.PlayerGame.FirstPlayed.Value < era.End)
                .ToList();

            var mergedGamesStarted = EnrichedGameHelper.MergeByConceptId(gamesStartedInEra);

            var containedGames = enrichedGames
                .Where(g =>
                    g.PlayerGame.FirstPlayed.HasValue &&
                    g.PlayerGame.LastPlayed.HasValue &&
                    g.PlayerGame.FirstPlayed.Value >= era.Start &&
                    g.PlayerGame.LastPlayed.Value < era.End)
                .ToList();

            var overlappedGames = enrichedGames
                .Where(g =>
                    g.PlayerGame.FirstPlayed.HasValue &&
                    g.PlayerGame.LastPlayed.HasValue &&
                    g.PlayerGame.FirstPlayed.Value < era.End &&
                    g.PlayerGame.LastPlayed.Value >= era.Start)
                .ToList();

            var longTailGames = distinctGames
                .Where(g =>
                g.PlayerGame.FirstPlayed.HasValue &&
                g.PlayerGame.LastPlayed.HasValue &&
                (g.PlayerGame.LastPlayed.Value - g.PlayerGame.FirstPlayed.Value).TotalDays >= 365)
                .Take(4)
                .ToList();

            var longTailStarts = longTailGames
                .Where(g =>
                    g.PlayerGame.FirstPlayed!.Value >= era.Start &&
                    g.PlayerGame.FirstPlayed.Value < era.End)
                .ToList();

            var longTailEnds = longTailGames
                .Where(g =>
                    g.PlayerGame.LastPlayed!.Value <= DateTime.Now.AddDays(-60) &&
                    g.PlayerGame.LastPlayed!.Value >= era.Start &&
                    g.PlayerGame.LastPlayed.Value < era.End)
                .ToList();

            var ea = new EraAnalytics
            {
                Start = era.Start,
                End = era.End,
                MergedGamesStarted = mergedGamesStarted,
                TotalGamesPlayed = containedGames.Count,
                UniqueGamesPlayed = EnrichedGameHelper.MergeByConceptId(containedGames).Count,
                TotalHoursPlayed = containedGames.Sum(g => g.PlayerGame.PlayHours!.Value),
                TotalSessionsPlayed = containedGames
                    .Where(g => g.PlayerGame.PlayCount.HasValue)
                    .Sum(g => g.PlayerGame.PlayCount!.Value),

                MedianHoursPerGame = PSNAnalyticsService.CalculateMedian(containedGames.Select(g => g.PlayerGame.PlayHours!.Value))
            };

            ea.AverageSessionLength = ea.TotalSessionsPlayed > 0
                ? ea.TotalHoursPlayed / ea.TotalSessionsPlayed
                : 0;

            ea.HoursPerMonth = era.MonthCount > 0
                ? ea.TotalHoursPlayed / era.MonthCount
                : 0;

            ea.PS4GamesPlayed = overlappedGames.Count(
                g => g.PlayerGame.Platform?.Equals("PS4", StringComparison.OrdinalIgnoreCase) ?? false);

            ea.PS5GamesPlayed = overlappedGames.Count(
                g => g.PlayerGame.Platform?.Equals("PS5", StringComparison.OrdinalIgnoreCase) ?? false);

            ea.IGDBAnalytics.Summary.GenreCount = overlappedGames
                    .SelectMany(g => g.IGDBGame?.Genres ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

            ea.IGDBAnalytics.Summary.ThemeCount = overlappedGames
                    .SelectMany(g => g.IGDBGame?.Themes ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

            ea.IGDBAnalytics.Summary.DeveloperCount = overlappedGames
                    .SelectMany(g => g.IGDBGame?.Developers ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

            ea.IGDBAnalytics.Summary.PublisherCount = overlappedGames
                    .SelectMany(g => g.IGDBGame?.Publishers ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

            ea.IGDBAnalytics.Summary.FranchiseCount = overlappedGames
                    .SelectMany(g => g.IGDBGame?.Franchises ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

            ea.IGDBAnalytics ??= new();
            ea.IGDBAnalytics.FranchiseAnalytics = IGDBAnalyticsService.CalculateFranchiseAnalytics(gamesStartedInEra, ea.IGDBAnalytics.Summary.FranchiseCount);
            ea.IGDBAnalytics.GameModeAnalytics = IGDBAnalyticsService.CalculateGameModeAnalytics(gamesStartedInEra);
            ea.IGDBAnalytics.GenreAnalytics = IGDBAnalyticsService.CalculateGenreAnalytics(gamesStartedInEra);
            ea.IGDBAnalytics.ThemeAnalytics = IGDBAnalyticsService.CalculateThemeAnalytics(gamesStartedInEra);
            ea.IGDBAnalytics.DeveloperAnalytics = IGDBAnalyticsService.CalculateDeveloperAnalytics(gamesStartedInEra, ea.IGDBAnalytics.Summary.DeveloperCount);
            ea.IGDBAnalytics.PublisherAnalytics = IGDBAnalyticsService.CalculatePublisherAnalytics(gamesStartedInEra, ea.IGDBAnalytics.Summary.PublisherCount);
            ea.IGDBAnalytics.AgeRatingAnalytics = IGDBAnalyticsService.CalculateAgeRatingAnalytics(gamesStartedInEra);
            ea.IGDBAnalytics.ReleaseDateAnalytics = IGDBAnalyticsService.CalculateReleaseDateAnalytics(gamesStartedInEra);
            ea.IGDBAnalytics.ReviewRatingAnalytics = IGDBAnalyticsService.CalculateReviewRatingAnalytics(gamesStartedInEra, maxGamesPerTier: 10);

            // Last game before era (by LastPlayed).
            var lastGameBefore = enrichedGames
                .Where(g =>
                    g.PlayerGame.LastPlayed.HasValue &&
                    g.PlayerGame.LastPlayed.Value < era.Start)
                .OrderByDescending(g => g.PlayerGame.LastPlayed)
                .FirstOrDefault();

            // First game after era (by FirstPlayed).
            var firstGameAfter = enrichedGames
                .Where(g =>
                    g.PlayerGame.FirstPlayed.HasValue &&
                    g.PlayerGame.FirstPlayed.Value >= era.End)
                .OrderBy(g => g.PlayerGame.FirstPlayed)
                .FirstOrDefault();

            // Build the drought detection set.
            var gamesForDroughtDetection = new List<EnrichedPlayerGame>();
            if (lastGameBefore is not null) gamesForDroughtDetection.Add(lastGameBefore);
            gamesForDroughtDetection.AddRange(gamesStartedInEra);
            if (firstGameAfter is not null) gamesForDroughtDetection.Add(firstGameAfter);

            // Run the drought detector, then filter to droughts that started in this era.
            var newGameDrought = PSNAnalyticsService
                .CalculateNewGameDroughts(gamesForDroughtDetection)
                .Where(d => d.StartDate >= era.Start && d.StartDate < era.End)
                .OrderByDescending(d => d.DurationDays)
                .FirstOrDefault();

            if (newGameDrought is not null)
                ea.NewGameDroughts = newGameDrought;

            ea.CompletedGames = [.. enrichedGames
                .Where(g =>
                    g.PlayerGame.TrophyData is { Count: > 0 } &&
                    g.PlayerGame.TrophyData.FirstOrDefault(t =>
                        (t.Progress >= 100 || t.EarnedTrophies.Platinum > 0) &&
                        t.LastTrophyEarned.HasValue &&
                        t.LastTrophyEarned.Value >= era.Start &&
                        t.LastTrophyEarned.Value < era.End) is not null)
                .OrderBy(g => g.PlayerGame.TrophyData.FirstOrDefault()!.LastTrophyEarned)];

            // ── Curated lists ─────────────────────────────────────────
            ea.MostPlayedGames = PSNAnalyticsService.CalculateMostPlayedGames(mergedGamesStarted);
            ea.LongTailStarts = longTailStarts;
            ea.LongTailEnds = longTailEnds;
            ea.AbandonedGames = PSNAnalyticsService.CalculateAbandonedGames(gamesStartedInEra);
            ea.SingleDayGames = PSNAnalyticsService.CalculateSingleDayGames(gamesStartedInEra);

            eraAnalytics.Add(ea);
        }

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(
                eraAnalytics,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                });

            var path = Path.Combine(Directory.GetCurrentDirectory(), "eraAnalytics.json");
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write era analytics: {ex.Message}");
        }

        return eraAnalytics;
    }

    private static Map PopulateMap(Map map, List<EraAnalytics> eraAnalytics)
    {
    var pairs = map.Eras
        .Zip(eraAnalytics, (era, analytics) => (era, analytics))
        .ToList();

    var selected = EraComponentSelector.SelectDiverse(pairs);

    // If the diversity pass returned nothing, fall back to per-era selection
    // so the map isn't empty. Better to overfill than to render nothing.
    if (selected.Count == 0)
    {
        Console.WriteLine("[PopulateMap] WARN: SelectDiverse returned 0, falling back");
        foreach (var era in map.Eras)
            era.Components = [];
        for (var i = 0; i < pairs.Count; i++)
            pairs[i].era.Components = EraComponentSelector.Select(pairs[i].analytics);
        return map;
    }

    foreach (var era in map.Eras)
        era.Components = [];
    foreach (var (era, component) in selected)
        era.Components.Add(component);

    // Annotate each era using a mini LLM with guard rails
    return map;
    }
}