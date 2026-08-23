using MyGamingMap.API.Models.DTOs;

namespace MyGamingMap.API.Services;

public class IGDBAnalyticsService
{
    private const int MostPlayedGamesCount = 5;
    private const int ReleaseGapGamesCount = 5;
    private const int ReviewGamesCount = 5;
    private const double HighPercentile = 0.75;
    private const double LowPercentile = 0.25;
    private const int MaxGamesPerTier = 200;

    public IGDB_Analytics CalculateIGDBAnalytics(IEnumerable<EnrichedPlayerGame> enrichedGames)
    {
        var games = enrichedGames
            .Where(g => g.IGDBGame != null)
            .ToList();

        return new IGDB_Analytics
        {
            FranchiseAnalytics = CalculateFranchiseAnalytics(games),
            GameEngineAnalytics = CalculateGameEngineAnalytics(games),
            GameModeAnalytics = CalculateGameModeAnalytics(games),
            GenreAnalytics = CalculateGenreAnalytics(games),
            ThemeAnalytics = CalculateThemeAnalytics(games),
            DeveloperAnalytics = CalculateDeveloperAnalytics(games),
            PublisherAnalytics = CalculatePublisherAnalytics(games),
            AgeRatingAnalytics = CalculateAgeRatingAnalytics(games),
            ReleaseDateAnalytics = CalculateReleaseDateAnalytics(games),
            ReviewRatingAnalytics = CalculateReviewRatingAnalytics(games)
        };
    }

    private static List<FranchiseAnalytic> CalculateFranchiseAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        var gameList = games.ToList();

        // Get the unique set of games associated with each franchise
        var franchiseGameSets = gameList
            .SelectMany(game => game.IGDBGame!.Franchises
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(franchise => new
                {
                    Franchise = franchise,
                    Game = game
                }))
            .GroupBy(x => x.Franchise, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                group.First().Franchise,
                Games = group
                    .Select(x => x.Game)
                    .DistinctBy(GetConceptGroupKey)
                    .ToList()
            })
            .ToList();

        // Find franchises which contain exactly the same games
        var franchiseGroups = franchiseGameSets
            .GroupBy(x =>
                string.Join(
                    "|",
                    x.Games
                        .Select(GetConceptGroupKey)
                        .OrderBy(id => id)))
            .ToList();

        // Map every original franchise name to its merged name
        var franchiseNameMap = franchiseGroups
            .SelectMany(group =>
            {
                var mergedName = string.Join(
                    " / ",
                    group
                        .Select(x => x.Franchise)
                        .OrderBy(x => x));

                return group.Select(x => new
                {
                    OriginalName = x.Franchise,
                    MergedName = mergedName
                });
            })
            .ToDictionary(
                x => x.OriginalName,
                x => x.MergedName,
                StringComparer.OrdinalIgnoreCase);

        return [.. CalculateCategoryAnalytics(
            gameList,
            game => game.IGDBGame!.Franchises
                .Where(f => franchiseNameMap.ContainsKey(f))
                .Select(f => franchiseNameMap[f])
                .Distinct(),
            (name, categoryGames) => new FranchiseAnalytic
            {
                Name = name,

                GamesStartedPerYear = CalculateGamesStartedPerYear(categoryGames),

                MostPlayedGames = CalculateMostPlayedGames(categoryGames),

                FirstPlayedYear = categoryGames
                    .Where(g => g.PlayerGame.FirstPlayed.HasValue)
                    .Min(g => (int?)g.PlayerGame.FirstPlayed!.Value.Year),

                LastPlayedYear = categoryGames
                    .Where(g => g.PlayerGame.LastPlayed.HasValue)
                    .Max(g => (int?)g.PlayerGame.LastPlayed!.Value.Year)
            })
            .OrderByDescending(x => x.CategoryRelevance)
            .Take(10)];
    }

    private static List<GameEngineAnalytic> CalculateGameEngineAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        return [.. CalculateCategoryAnalytics(
            games,
            game => game.IGDBGame!.GameEngines,
            (name, categoryGames) => new GameEngineAnalytic
            {
                Name = name,
            }
        )
        .OrderByDescending(x => x.CategoryRelevance)
        .Take(10)];
    }

    private static List<GameModeAnalytic> CalculateGameModeAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        return [.. CalculateCategoryAnalytics(
            games,
            game => game.IGDBGame!.GameModes,
            (name, categoryGames) => new GameModeAnalytic
            {
                Name = name,
                GamesStartedPerYear = CalculateGamesStartedPerYear(categoryGames),
            }
        )
        .OrderByDescending(x => x.HoursPlayed)];
    }

    private static List<GenreAnalytic> CalculateGenreAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        return [.. CalculateCategoryAnalytics(
            games,
            game => game.IGDBGame!.Genres,
            (name, categoryGames) => new GenreAnalytic
            {
                Name = name,
                GamesStartedPerYear = CalculateGamesStartedPerYear(categoryGames),
                MostPlayedGames = CalculateMostPlayedGames(categoryGames),
            }
        )
        .OrderByDescending(x => x.CategoryRelevance)
        .Take(10)];
    }

    private static List<ThemeAnalytic> CalculateThemeAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        return [.. CalculateCategoryAnalytics(
            games,
            game => game.IGDBGame!.Themes,
            (name, categoryGames) => new ThemeAnalytic
            {
                Name = name,
                GamesStartedPerYear = CalculateGamesStartedPerYear(categoryGames),
                MostPlayedGames = CalculateMostPlayedGames(categoryGames),
            }
        )
        .OrderByDescending(x => x.CategoryRelevance)
        .Take(10)];
    }

    private static List<CompanyAnalytic> CalculateDeveloperAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        return [.. CalculateCategoryAnalytics(
            games,
            game => game.IGDBGame!.Developers,
            (name, categoryGames) => new CompanyAnalytic
            {
                Name = name,

                FirstPlayedYear = categoryGames
                .Where(g => g.PlayerGame.FirstPlayed.HasValue)
                .Min(g => (int?)g.PlayerGame.FirstPlayed!.Value.Year),

                LastPlayedYear = categoryGames
                    .Where(g => g.PlayerGame.LastPlayed.HasValue)
                    .Max(g => (int?)g.PlayerGame.LastPlayed!.Value.Year)
            }
        )
        .OrderByDescending(x => x.CategoryRelevance)
        .Take(10)];
    }

    private static List<CompanyAnalytic> CalculatePublisherAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        return [.. CalculateCategoryAnalytics(
            games,
            game => game.IGDBGame!.Publishers,
            (name, categoryGames) => new CompanyAnalytic
            {
                Name = name
            }
        )
        .OrderByDescending(x => x.CategoryRelevance)
        .Take(10)];
    }

    private static AgeRatingAnalytics CalculateAgeRatingAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        var gameList = games.ToList();

        var pegiRatings = gameList
            .Where(g =>
                g.IGDBGame?.PEGI_Rating.HasValue == true &&
                g.IGDBGame.PEGI_Rating.Value > 0)
            .Select(g => (double)g.IGDBGame!.PEGI_Rating!.Value)
            .ToList();

        var gamesWithWeightedPEGI = gameList
            .Where(g =>
                g.IGDBGame?.PEGI_Rating.HasValue == true &&
                g.IGDBGame.PEGI_Rating.Value > 0 &&
                g.PlayerGame.PlayHours.HasValue &&
                g.PlayerGame.PlayHours.Value > 0)
            .ToList();

        var totalPlaytime = gamesWithWeightedPEGI.Sum(g => g.PlayerGame.PlayHours!.Value);

        return new AgeRatingAnalytics
        {
            ESRBRatingAnalytics = [.. CalculateCategoryAnalytics(
                gameList,
                game => string.IsNullOrWhiteSpace(game.IGDBGame!.ESRB_Rating)
                    ? []
                    : [game.IGDBGame.ESRB_Rating],
                (name, categoryGames) => new AgeRatingAnalytic
                {
                    Name = name,
                    GamesStartedPerYear = CalculateGamesStartedPerYear(categoryGames),
                }
            )
            .OrderByDescending(x => x.GamesPlayed)],

            PEGIRatingAnalytics = [.. CalculateCategoryAnalytics(
                gameList,
                game => game.IGDBGame!.PEGI_Rating.HasValue &&
                        game.IGDBGame.PEGI_Rating.Value > 0
                    ? [game.IGDBGame.PEGI_Rating.Value.ToString()!]
                    : [],
                (name, categoryGames) => new AgeRatingAnalytic
                {
                    Name = name,
                    GamesStartedPerYear = CalculateGamesStartedPerYear(categoryGames),
                }
            )
            .OrderByDescending(x => x.GamesPlayed)],

            AveragePEGIRating = CalculateAverage(pegiRatings),

            PlaytimeWeightedAgeRating = totalPlaytime > 0
                ? gamesWithWeightedPEGI.Sum(g =>
                    g.IGDBGame!.PEGI_Rating!.Value *
                    g.PlayerGame.PlayHours!.Value
                ) / totalPlaytime
                : 0
        };
    }

    private static ReleaseDateAnalytics CalculateReleaseDateAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        var gameList = games.ToList();

        var releaseDates = gameList.ToDictionary(game => game, GetReleaseDate);

        var gamesWithReleaseDates = gameList
            .Where(g => releaseDates[g].HasValue)
            .ToList();

        var gaps = games
            .Where(g =>
                releaseDates[g].HasValue &&
                g.PlayerGame.FirstPlayed.HasValue)
            .Select(g => new
            {
                Game = g,
                GapDays = (
                    g.PlayerGame.FirstPlayed!.Value.Date -
                    releaseDates[g]!.Value.ToDateTime(TimeOnly.MinValue)
                ).TotalDays
            })
            .Where(x => x.GapDays >= 0)
            .ToList();

        return new ReleaseDateAnalytics
        {
            ReleaseYearAnalytics = [.. CalculateCategoryAnalytics(
                gamesWithReleaseDates,
                game => [releaseDates[game]!.Value.Year.ToString()],
                (name, categoryGames) => new ReleaseYearAnalytic
                {
                    Name = name,
                    GamesStartedPerYear = CalculateGamesStartedPerYear(categoryGames),
                }
            )
            .OrderBy(x => int.Parse(x.Name))],

            AverageReleaseDate = CalculateAverageReleaseDate(
                gamesWithReleaseDates.Select(g => releaseDates[g]!.Value)),

            AverageReleaseToFirstPlayedTimeDays = gaps.Count > 0
                ? gaps.Average(x => x.GapDays)
                : 0,

            PlayedSoonAfterRelease = [.. gaps
            .OrderBy(x => x.GapDays)
            .Take(ReleaseGapGamesCount)
            .Select(x => new ReleaseGapGame
            {
                Game = x.Game,
                ReleaseDate = releaseDates[x.Game]!.Value,
                DaysAfterRelease = x.GapDays
            })],

            PlayedLongAfterRelease = [.. gaps
            .OrderByDescending(x => x.GapDays)
            .Take(ReleaseGapGamesCount)
            .Select(x => new ReleaseGapGame
            {
                Game = x.Game,
                ReleaseDate = releaseDates[x.Game]!.Value,
                DaysAfterRelease = x.GapDays
            })]
        };
    }

    private static ReviewRatingAnalytics CalculateReviewRatingAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        var gamesWithRatings = games
            .Where(g => g.IGDBGame?.ReviewRating.HasValue == true)
            .ToList();

        if (gamesWithRatings.Count == 0) return new ReviewRatingAnalytics();

        var tiers = new[]
        {
            new { Name = "S", Min = 90.0, Max = double.MaxValue },
            new { Name = "A", Min = 85.0, Max = 90.0 },
            new { Name = "B", Min = 77.5, Max = 85.0 },
            new { Name = "C", Min = 70.0, Max = 77.5 },
            new { Name = "D", Min = 65.0, Max = 70.0 },
            new { Name = "E", Min = 60.0, Max = 65.0 },
            new { Name = "F", Min = double.MinValue, Max = 60.0 }
        };

        var ratingTiers = tiers
            .Select(tier =>
            {
                var tierGames = gamesWithRatings
                .Where(g =>
                    g.IGDBGame!.ReviewRating!.Value >= tier.Min &&
                    g.IGDBGame.ReviewRating.Value < tier.Max)
                .OrderByDescending(g =>
                    g.IGDBGame!.ReviewCount.GetValueOrDefault())
                .Take(MaxGamesPerTier)
                .OrderByDescending(g =>
                    g.IGDBGame!.ReviewRating!.Value)
                .ToList();

                return new ReviewRatingTier
                {
                    Name = tier.Name,
                    GamesPlayed = tierGames.Count,
                    Games = tierGames
                };
            })
            .ToList();

        var gamesWithPlaytime = gamesWithRatings
            .Where(g => g.PlayerGame.PlayHours.HasValue)
            .ToList();

        var ratings = gamesWithRatings
            .Select(g => g.IGDBGame!.ReviewRating!.Value)
            .OrderBy(x => x)
            .ToList();

        var playtimes = gamesWithPlaytime
            .Select(g => g.PlayerGame.PlayHours!.Value)
            .OrderBy(x => x)
            .ToList();

        var totalPlaytime = gamesWithPlaytime.Sum(g => g.PlayerGame.PlayHours!.Value);

        var highRatingThreshold = CalculatePercentile(ratings, HighPercentile);
        var lowRatingThreshold = CalculatePercentile(ratings, LowPercentile);

        var highPlaytimeThreshold = CalculatePercentile(playtimes, HighPercentile);
        var lowPlaytimeThreshold = CalculatePercentile(playtimes, LowPercentile);

        return new ReviewRatingAnalytics
        {
            RatingTiers = ratingTiers,

            HighestRatedGames = [.. gamesWithRatings
            .OrderByDescending(g => g.IGDBGame!.ReviewRating!.Value)
            .Take(ReviewGamesCount)],

            LowestRatedGames = [.. gamesWithRatings
            .OrderBy(g => g.IGDBGame!.ReviewRating!.Value)
            .Take(ReviewGamesCount)],

            HighRatingLowPlaytime = [.. gamesWithPlaytime
            .Where(g =>
                g.IGDBGame!.ReviewRating!.Value >= highRatingThreshold &&
                g.PlayerGame.PlayHours!.Value <= lowPlaytimeThreshold)
            .OrderByDescending(g => g.IGDBGame!.ReviewRating!.Value)
            .ThenBy(g => g.PlayerGame.PlayHours!.Value)
            .Take(ReviewGamesCount)],

            LowRatingHighPlaytime = [.. gamesWithPlaytime
            .Where(g =>
                g.IGDBGame!.ReviewRating!.Value <= lowRatingThreshold &&
                g.PlayerGame.PlayHours!.Value >= highPlaytimeThreshold)
            .OrderBy(g => g.PlayerGame.PlayHours!.Value)
            .ThenBy(g => g.IGDBGame!.ReviewRating!.Value)
            .Take(ReviewGamesCount)],

            AverageReviewRating = ratings.Average(),

            PlaytimeWeightedReviewRating = totalPlaytime > 0
            ? gamesWithPlaytime.Sum(g =>
                g.IGDBGame!.ReviewRating!.Value *
                g.PlayerGame.PlayHours!.Value
            ) / totalPlaytime
            : 0
        };
    }

    private static List<TAnalytic> CalculateCategoryAnalytics<TAnalytic>(
        IEnumerable<EnrichedPlayerGame> games,
        Func<EnrichedPlayerGame, IEnumerable<string>> categorySelector,
        Func<string, List<EnrichedPlayerGame>, TAnalytic> analyticFactory)
        where TAnalytic : CategoryAnalytic
    {
        var gameList = games.ToList();

        var totalGames = gameList.Count;

        var totalHours = gameList
                .Where(g => g.PlayerGame.PlayHours.HasValue)
                .Sum(g => g.PlayerGame.PlayHours!.Value);

        var groups = games
            .SelectMany(game =>
            {
                var categories = categorySelector(game);

                if (categories == null)
                    return [];

                return categories
                    .Where(category => !string.IsNullOrWhiteSpace(category))
                    .Select(category => new
                    {
                        Category = category,
                        Game = game
                    });
            })
            .GroupBy(
                x => x.Category,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = groups
            .Select(group =>
            {
                var categoryGames = group
                    .Select(x => x.Game)
                    .ToList();

                var gamesWithPlaytime = categoryGames
                    .Where(g => g.PlayerGame.PlayHours.HasValue)
                    .ToList();

                var gamesWithSessions = categoryGames
                    .Where(g => g.PlayerGame.PlayCount.HasValue)
                    .ToList();

                var analytic = analyticFactory(group.First().Category, categoryGames);

                analytic.GamesPlayed = categoryGames.Count;
                analytic.HoursPlayed = gamesWithPlaytime.Sum(g => g.PlayerGame.PlayHours!.Value);
                analytic.SessionsPlayed = gamesWithSessions.Sum(g => g.PlayerGame.PlayCount!.Value);

                analytic.AverageHoursPerGame = CalculateAverage(
                    gamesWithPlaytime.Select(
                        g => g.PlayerGame.PlayHours!.Value));

                analytic.MedianHoursPerGame = CalculateMedian(
                    gamesWithPlaytime.Select(
                        g => g.PlayerGame.PlayHours!.Value));

                analytic.AverageSessionsPerGame = CalculateAverage(
                    gamesWithSessions.Select(
                        g => (double)g.PlayerGame.PlayCount!.Value));

                analytic.MedianSessionsPerGame = CalculateMedian(
                    gamesWithSessions.Select(
                        g => (double)g.PlayerGame.PlayCount!.Value));

                analytic.AverageSessionLength = CalculateAverageSessionLength(categoryGames);

                var trophyGames = categoryGames
                    .SelectMany(g => g.PlayerGame.TrophyData.Select(t => new TrophyGame
                    {
                        Game = g,
                        Trophy = t
                    }))
                    .Where(x => x.Trophy.Progress > 0)
                    .ToList();

                analytic.TotalCompletion = TrophyAnalyticsHelper.CalculateTotalCompletion(trophyGames.Select(x => x.Trophy));

                var progress = trophyGames
                    .Select(x => (double)x.Trophy.Progress)
                    .ToList();

                analytic.AverageCompletion = CalculateAverage(progress);

                analytic.GamesCompleted =
                    trophyGames
                        .Where(x => x.Trophy.Progress >= 100)
                        .Select(TrophyAnalyticsHelper.GetGameKey)
                        .Count();

                analytic.PlatinumsEarned =
                    trophyGames.Count(x =>
                        x.Trophy.EarnedTrophies.Platinum >= 1);

                analytic.PlatinumsAvailable =
                    trophyGames.Count(x =>
                        x.Trophy.DefinedTrophies.Platinum > 0);

                analytic.PlatinumRate =
                    analytic.PlatinumsAvailable > 0
                        ? (double)analytic.PlatinumsEarned /
                          analytic.PlatinumsAvailable * 100
                        : 0;

                analytic.PlatinumRate = analytic.PlatinumsAvailable > 0
                    ? (double)analytic.PlatinumsEarned / analytic.PlatinumsAvailable * 100
                    : 0;

                analytic.Breadth = totalGames > 0 ?
                    (double)analytic.GamesPlayed / totalGames * 100
                    : 0;

                analytic.Investment = totalHours > 0
                    ? analytic.HoursPlayed / totalHours * 100
                    : 0;

                analytic.CategoryRelevance = (analytic.Breadth * 0.5) + (analytic.Investment * 0.5);

                //analytic.Games = categoryGames;

                return analytic;
            });

        return [.. results];
    }

    private static List<MostPlayedGame> CalculateMostPlayedGames(IEnumerable<EnrichedPlayerGame> games)
    {
        return [.. games
            .GroupBy(GetConceptGroupKey)
            .Select(group =>
            {
                var gamesInConcept = group.ToList();

                var representative = gamesInConcept
                    .OrderByDescending(g =>
                        g.PlayerGame.PlayHours ?? 0)
                    .First();

                var totalHours = gamesInConcept.Sum(g => g.PlayerGame.PlayHours ?? 0);

                var totalSessions = gamesInConcept.Sum(g => g.PlayerGame.PlayCount ?? 0);

                var categoryTotalHours = games.ToList().Sum(g => g.PlayerGame.PlayHours ?? 0);

                return new MostPlayedGame
                {
                    Game = representative,
                    HoursPlayed = totalHours,
                    SessionsPlayed = totalSessions,
                    PercentageOfTotalPlaytime = categoryTotalHours > 0 ? totalHours / categoryTotalHours * 100 : 0
                };
            })
            .OrderByDescending(g => g.HoursPlayed)
            .Take(MostPlayedGamesCount)];
    }

    private static List<GamesStartedByYear> CalculateGamesStartedPerYear(IEnumerable<EnrichedPlayerGame> games)
    {
        return [.. games
            .Where(g => g.PlayerGame.FirstPlayed.HasValue)
            .GroupBy(g => g.PlayerGame.FirstPlayed!.Value.Year)
            .OrderBy(g => g.Key)
            .Select(g => new GamesStartedByYear
            {
                Year = g.Key,
                GamesStarted = g.Count()
            })];
    }

    private static double CalculateAverageSessionLength(IEnumerable<EnrichedPlayerGame> games)
    {
        var totalHours = games.Sum(g => g.PlayerGame.PlayHours ?? 0);
        var totalSessions = games.Sum(g => g.PlayerGame.PlayCount ?? 0);

        return totalSessions > 0
            ? totalHours / totalSessions
            : 0;
    }

    private static long GetConceptGroupKey(EnrichedPlayerGame game)
    {
        return game.PlayerGame.ConceptId ?? game.IGDBGame!.IGDB_Id;
    }

    private static double CalculateAverage(IEnumerable<double> values)
    {
        var list = values.ToList();

        return list.Count > 0 ? list.Average() : 0;
    }

    private static double CalculateMedian(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(x => x).ToList();

        if (ordered.Count == 0) return 0;

        int middle = ordered.Count / 2;

        return ordered.Count % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2
            : ordered[middle];
    }

    private static DateOnly? GetReleaseDate(EnrichedPlayerGame game)
    {
        var releaseDates = game.IGDBGame?.ReleaseDates;

        if (releaseDates == null || releaseDates.Count == 0) return null;

        var firstPlayed = game.PlayerGame.FirstPlayed?.Date;

        // First prioritise the player's platform.
        var platformDates = releaseDates
            .Where(r =>
                r.Date.HasValue &&
                string.Equals(
                    r.Platform,
                    game.PlayerGame.Platform,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        // If there are no release dates for that platform,
        // fall back to release dates for all platforms.
        var candidates = platformDates.Count > 0
            ? platformDates
            : [.. releaseDates.Where(r => r.Date.HasValue)];

        if (candidates.Count == 0) return null;

        // If we know when the player first played, first check for
        // a release date on exactly that date.
        if (firstPlayed.HasValue)
        {
            var firstPlayedDate = DateOnly.FromDateTime(firstPlayed.Value);

            var exactMatch = candidates
                .Where(r => r.Date!.Value == firstPlayedDate)
                .ToList();

            if (exactMatch.Count > 0)
            {
                var worldwideExactMatch = exactMatch
                    .Where(r =>
                        string.Equals(
                            r.Region,
                            "worldwide",
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return (worldwideExactMatch.Count > 0
                        ? worldwideExactMatch
                        : exactMatch)
                    .First()
                    .Date;
            }

            // Otherwise, only consider releases before the player first played.
            candidates = [.. candidates.Where(r => r.Date!.Value < firstPlayedDate)];

            if (candidates.Count == 0) return null;
        }

        // Prefer worldwide when multiple release dates are otherwise valid.
        var worldwide = candidates
            .Where(r =>
                string.Equals(
                    r.Region,
                    "worldwide",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (worldwide.Count > 0) candidates = worldwide;

        // Choose the earliest possible release date.
        return candidates
            .OrderBy(r => r.Date)
            .First()
            .Date;
    }

    private static DateOnly CalculateAverageReleaseDate(IEnumerable<DateOnly> releaseDates)
    {
        var dates = releaseDates.ToList();

        if (dates.Count == 0) return default;

        var averageDayNumber = (int)Math.Round(dates.Average(d => d.DayNumber));

        return DateOnly.FromDayNumber(averageDayNumber);
    }

    private static double CalculatePercentile(IEnumerable<double> values, double percentile)
    {
        var ordered = values
            .OrderBy(x => x)
            .ToList();

        if (ordered.Count == 0) return 0;

        if (ordered.Count == 1) return ordered[0];

        var position = (ordered.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);

        if (lower == upper) return ordered[lower];

        var fraction = position - lower;

        return ordered[lower] + (ordered[upper] - ordered[lower]) * fraction;
    }
}