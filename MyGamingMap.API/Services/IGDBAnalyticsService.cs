using MyGamingMap.API.Models.DTOs;

namespace MyGamingMap.API.Services;

public class IGDBAnalyticsService(DatabaseService databaseService)
{
    private readonly DatabaseService databaseService = databaseService;
    private int franchiseCount;
    private int gameEngineCount;
    private int genreCount;
    private int themeCount;
    private int developerCount;
    private int publisherCount;

    public async Task<IGDB_Analytics> CalculateIGDBAnalytics(IEnumerable<EnrichedPlayerGame> enrichedGames)
    {
        var games = enrichedGames
            .Where(g => g.IGDBGame != null)
            .ToList();

        foreach (var game in games)
        {
            game.IGDBGame!.ReviewRating = NormaliseReviewRating(game.IGDBGame.ReviewRating, game.IGDBGame.ReviewCount);
        }

        var distinctGames = EnrichedGameHelper.MergeByConceptId(games);

        franchiseCount = distinctGames
                    .SelectMany(g => g.IGDBGame!.Franchises)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

        gameEngineCount = distinctGames
                    .SelectMany(g => g.IGDBGame!.GameEngines)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

        genreCount = distinctGames
                    .SelectMany(g => g.IGDBGame!.Genres)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

        themeCount = distinctGames
                    .SelectMany(g => g.IGDBGame!.Themes)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

        developerCount = distinctGames
                    .SelectMany(g => g.IGDBGame!.Developers)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

        publisherCount = distinctGames
                    .SelectMany(g => g.IGDBGame!.Publishers)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

        var analytics = new IGDB_Analytics
        {
            Summary = new Summary
            {
                FranchiseCount = franchiseCount,
                GameEngineCount = gameEngineCount,
                GenreCount = genreCount,
                ThemeCount = themeCount,
                DeveloperCount = developerCount,
                PublisherCount = publisherCount
            },

            FranchiseAnalytics = CalculateFranchiseAnalytics(games), // Preserves platform specific completion data
            GameEngineAnalytics = CalculateGameEngineAnalytics(games),
            GameModeAnalytics = CalculateGameModeAnalytics(games),
            GenreAnalytics = CalculateGenreAnalytics(games),
            ThemeAnalytics = CalculateThemeAnalytics(games),
            DeveloperAnalytics = CalculateDeveloperAnalytics(games),
            PublisherAnalytics = CalculatePublisherAnalytics(games),
            AgeRatingAnalytics = CalculateAgeRatingAnalytics(games),
            ReleaseDateAnalytics = CalculateReleaseDateAnalytics(games), // Preserves platform specific release dates
            ReviewRatingAnalytics = CalculateReviewRatingAnalytics(games, distinctGames)
        };

        analytics.TasteProfile = CalculateTasteProfile(analytics);
        analytics.TasteProfile.UnexploredGenres = await databaseService.GetUnexploredGenres(distinctGames);
        analytics.TasteProfile.UnexploredThemes = await databaseService.GetUnexploredThemes(distinctGames);

        return analytics;
    }

    private List<FranchiseAnalytic> CalculateFranchiseAnalytics(IEnumerable<EnrichedPlayerGame> games)
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

        var topFranchisesCount = franchiseCount switch
        {
            < 15 => 1,
            < 25 => 3,
            < 50 => 5,
            _ => 10
        };

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

                MostPlayedGames = CalculateMostPlayedGames(EnrichedGameHelper.MergeByConceptId(categoryGames)),

                FirstPlayedYear = categoryGames
                    .Where(g => g.PlayerGame.FirstPlayed.HasValue)
                    .Min(g => (int?)g.PlayerGame.FirstPlayed!.Value.Year),

                LastPlayedYear = categoryGames
                    .Where(g => g.PlayerGame.LastPlayed.HasValue)
                    .Max(g => (int?)g.PlayerGame.LastPlayed!.Value.Year)
            })
            .OrderByDescending(x => x.CategoryRelevance)
            .Take(topFranchisesCount)];
    }

    private List<GameEngineAnalytic> CalculateGameEngineAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        var topGameEnginesCount = gameEngineCount switch
        {
            < 15 => 1,
            < 25 => 3,
            < 50 => 5,
            _ => 10
        };

        return [.. CalculateCategoryAnalytics(
            games,
            game => game.IGDBGame!.GameEngines,
            (name, categoryGames) => new GameEngineAnalytic
            {
                Name = name,
            }
        )
        .OrderByDescending(x => x.CategoryRelevance)
        .Take(topGameEnginesCount)];
    }

    private static List<GameModeAnalytic> CalculateGameModeAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        return [.. CalculateCategoryAnalytics(
            games,
            game =>
            {
                var modes = game.IGDBGame!.GameModes.ToList();

                var hasSinglePlayer = modes.Contains(
                    "Single player",
                    StringComparer.OrdinalIgnoreCase);

                if (hasSinglePlayer && modes.Count == 1)
                {
                    return ["Single player only"];
                }

                if (!hasSinglePlayer)
                {
                    return ["Multiplayer only"];
                }

                if (hasSinglePlayer && modes.Count > 1)
                {
                    return ["Single player / Multiplayer"];
                }

                return modes;
            },
            (name, categoryGames) => new GameModeAnalytic
            {
                Name = name,
                GamesStartedPerYear = CalculateGamesStartedPerYear(categoryGames),
            }
        )
        .OrderByDescending(x => x.CategoryRelevance)];
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
        .OrderByDescending(x => x.CategoryRelevance)];
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
        .OrderByDescending(x => x.CategoryRelevance)];
    }

    private List<CompanyAnalytic> CalculateDeveloperAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        var topDevelopersCount = developerCount switch
        {
            < 15 => 1,
            < 25 => 3,
            < 50 => 5,
            _ => 10
        };

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
        .Take(topDevelopersCount)];
    }

    private List<CompanyAnalytic> CalculatePublisherAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        var topPublishersCount = publisherCount switch
        {
            < 15 => 1,
            < 25 => 3,
            < 50 => 5,
            _ => 10
        };

        return [.. CalculateCategoryAnalytics(
            games,
            game => game.IGDBGame!.Publishers,
            (name, categoryGames) => new CompanyAnalytic
            {
                Name = name
            }
        )
        .OrderByDescending(x => x.CategoryRelevance)
        .Take(topPublishersCount)];
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
            .OrderByDescending(x => x.CategoryRelevance)],

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
            .OrderByDescending(x => x.CategoryRelevance)],

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

        const int ReleaseGapGamesCount = 5;

        return new ReleaseDateAnalytics
        {
            GamingAge = CalculateGamingAge(games, releaseDates),

            ReleaseYearAnalytics = [..CalculateCategoryAnalytics(
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

            PlaytimeWeightedAverageReleaseDate = CalculatePlaytimeWeightedAverageReleaseDate(gamesWithReleaseDates, releaseDates),

            AverageReleaseToFirstPlayedTimeDays = gaps.Count > 0
                ? gaps.Average(x => x.GapDays)
                : 0,

            PlayedSoonAfterRelease = [..gaps
            .OrderBy(x => x.GapDays)
            .Take(ReleaseGapGamesCount)
            .Select(x => new ReleaseGapGame
             {
                 Game = x.Game,
                 ReleaseDate = releaseDates[x.Game]!.Value,
                 DaysAfterRelease = x.GapDays
             })],

            PlayedLongAfterRelease = [..gaps
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

    private static ReviewRatingAnalytics CalculateReviewRatingAnalytics(IEnumerable<EnrichedPlayerGame> games, IEnumerable<EnrichedPlayerGame> distinctGames)
    {
        var gamesWithRatings = games
            .Where(g => g.IGDBGame?.ReviewRating.HasValue == true)
            .ToList();

        var distinctGamesWithRatings = distinctGames
            .Where(g => g.IGDBGame?.ReviewRating.HasValue == true)
            .ToList();

        if (gamesWithRatings.Count == 0) return new ReviewRatingAnalytics();

        var tiers = new[]
        {
            new { Name = "S", Min = 90.0, Max = double.MaxValue },
            new { Name = "A", Min = 82.0, Max = 90.0 },
            new { Name = "B", Min = 75.0, Max = 82.0 },
            new { Name = "C", Min = 69.0, Max = 75.0 },
            new { Name = "D", Min = 64.0, Max = 69.0 },
            new { Name = "E", Min = 60.0, Max = 64.0 },
            new { Name = "F", Min = double.MinValue, Max = 60.0 }
        };

        var ratingTiers = tiers
            .SelectMany(tier =>
            {
                // All games belonging to this rating tier.
                var allTierGames = distinctGamesWithRatings
                    .Where(g =>
                        g.IGDBGame!.ReviewRating!.Value >= tier.Min &&
                        g.IGDBGame.ReviewRating.Value < tier.Max)
                    .ToList();

                // Calculate analytics against the entire rated game set,
                // but only assign games belonging to this tier to the category.
                var analytics = CalculateCategoryAnalytics(
                    distinctGamesWithRatings,
                    game =>
                    {
                        var rating = game.IGDBGame!.ReviewRating!.Value;

                        return rating >= tier.Min && rating < tier.Max
                            ? [tier.Name]
                            : [];
                    },
                    (name, categoryGames) => new ReviewRatingTier
                    {
                        Name = name
                    }
                ).ToList();

                const int MaxGamesPerTier = 1;

                // Only merge the games that are actually stored.
                foreach (var tierAnalytics in analytics)
                {
                    tierAnalytics.Games = [.. allTierGames
                        .OrderByDescending(g => g.IGDBGame!.ReviewCount.GetValueOrDefault()) // Prioritises roughly by popularity 
                        .Take(MaxGamesPerTier)
                        .OrderByDescending(g => g.IGDBGame!.ReviewRating!.Value)];
                }

                return analytics;
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

        const double HighPercentile = 0.75;
        const double LowPercentile = 0.25;

        var highRatingThreshold = CalculatePercentile(ratings, HighPercentile);
        var lowRatingThreshold = 70; // D tier and below

        var highPlaytimeThreshold = CalculatePercentile(playtimes, HighPercentile);
        var lowPlaytimeThreshold = CalculatePercentile(playtimes, LowPercentile);

        const int ReviewGamesCount = 10;

        return new ReviewRatingAnalytics
        {
            RatingTiers = [.. ratingTiers],

            HighestRatedGames = [..distinctGamesWithRatings
            .OrderByDescending(g => g.IGDBGame!.ReviewRating!.Value)
            .Take(ReviewGamesCount)],

            LowestRatedGames = [..distinctGamesWithRatings
            .OrderBy(g => g.IGDBGame!.ReviewRating!.Value)
            .Take(ReviewGamesCount)],

            // Don't include games with less than 18 minutes play time (accidental launches, testing, etc.)
            // Don't include games played in last 2 weeks, as they may still be in progress
            HighRatingLowPlaytime = [..gamesWithPlaytime
            .Where(g =>
                g.IGDBGame!.ReviewRating!.Value >= highRatingThreshold &&
                g.PlayerGame.PlayHours!.Value <= lowPlaytimeThreshold &&
                g.PlayerGame.PlayHours!.Value >= 0.3 &&
                g.PlayerGame.LastPlayed <= DateTime.UtcNow.AddDays(-14))
            .OrderByDescending(g => g.IGDBGame!.ReviewRating!.Value)
            .ThenBy(g => g.PlayerGame.PlayHours!.Value)
            .Take(ReviewGamesCount)],

            LowRatingHighPlaytime = [..gamesWithPlaytime
            .Where(g =>
                g.IGDBGame!.ReviewRating!.Value <= lowRatingThreshold &&
                g.PlayerGame.PlayHours!.Value >= highPlaytimeThreshold)
            .OrderBy(g => g.PlayerGame.PlayHours!.Value)
            .ThenBy(g => g.IGDBGame!.ReviewRating!.Value)
            .Take(ReviewGamesCount)],

            AverageReviewRating = ratings.Average()
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

    private static TasteProfile CalculateTasteProfile(IGDB_Analytics analytics)
    {
        var ageRatingAnalytics = analytics.AgeRatingAnalytics ?? new AgeRatingAnalytics();

        return new TasteProfile
        {
            Genres = CalculateCategoryTasteProfile(analytics.GenreAnalytics),
            Themes = CalculateCategoryTasteProfile(analytics.ThemeAnalytics),
            GameModes = CalculateCategoryTasteProfile(analytics.GameModeAnalytics, 1),
            ESRBRating = CalculateCategoryTasteProfile(ageRatingAnalytics.ESRBRatingAnalytics, 1),
            PEGIRating = CalculateCategoryTasteProfile(ageRatingAnalytics.PEGIRatingAnalytics, 1),
        };
    }

    private static TasteProfileCategory CalculateCategoryTasteProfile(IEnumerable<CategoryAnalytic>? categories, int topCategoryCount = 2)
    {
        var categoryList = categories ?? [];

        var orderedCategories = categoryList
        .Where(x => x.CategoryRelevance > 0)
        .OrderByDescending(x => x.CategoryRelevance)
        .ToList();

        if (orderedCategories.Count == 0) return new TasteProfileCategory();

        var maxRelevance = orderedCategories[0].CategoryRelevance;
        var totalRelevance = orderedCategories.Sum(x => x.CategoryRelevance);

        // 1 = strongest category of that type, 0.25 = 25% as relevant as the strongest category
        var evidence = orderedCategories
            .Where(x => x.CategoryRelevance / maxRelevance >= 0.25)
            .Take(topCategoryCount)
            .Select(x => new TastePreference
            {
                Name = x.Name,
                Relevance = x.CategoryRelevance
            })
            .ToList();

        var evidenceRelevance = evidence.Sum(x => x.Relevance);

        return new TasteProfileCategory
        {
            Coverage = totalRelevance > 0
                ? evidenceRelevance / totalRelevance * 100
                : 0,

            Evidence = evidence
        };
    }

    private static List<MostPlayedGame> CalculateMostPlayedGames(IEnumerable<EnrichedPlayerGame> games)
    {
        games = games.Where(g => g.PlayerGame.PlayHours.HasValue);

        var mostPlayedGamesCount = games.Count() switch
        {
            < 15 => 1,
            < 25 => 3,
            < 50 => 5,
            _ => 10
        };

        var totalHoursPlayed = games.Sum(g => g.PlayerGame.PlayHours!.Value);

        return [.. games
            .Where(g => g.PlayerGame.PlayHours.HasValue)
            .Select(g => new MostPlayedGame
            {
                Game = g,
                PercentageOfTotalPlaytime = totalHoursPlayed > 0
                    ? g.PlayerGame.PlayHours!.Value / totalHoursPlayed * 100
                    : 0
            })
            .OrderByDescending(g => g.Game.PlayerGame.PlayHours)
            .Take(mostPlayedGamesCount)
        ];
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

    private static double CalculateGamingAge(IEnumerable<EnrichedPlayerGame> games, Dictionary<EnrichedPlayerGame, DateOnly?> releaseDates)
    {
        var gameList = games
            .Where(g =>
                releaseDates.TryGetValue(g, out var releaseDate) &&
                releaseDate.HasValue)
            .ToList();

        if (gameList.Count == 0) return 0;

        // Use log-scaled playtime where available.
        // Games without playtime data, such as PS3/Vita games, still contribute to the player's gaming era.
        static double GetWeight(EnrichedPlayerGame game)
        {
            var playHours = game.PlayerGame.PlayHours.GetValueOrDefault();
            return playHours > 0 ? Math.Pow(playHours, 0.6) : 1.0;
        }

        // Calculate the total weight for each release year.
        var yearlyWeight = gameList
            .GroupBy(g => releaseDates[g]!.Value.Year)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(GetWeight));

        var minYear = yearlyWeight.Keys.Min();
        var maxYear = yearlyWeight.Keys.Max();

        const int windowSize = 5;

        // Find the consecutive x-year window with the greatest weight.
        var bestStartYear = minYear;
        var bestWindowWeight = 0.0;

        for (var startYear = minYear; startYear <= maxYear - windowSize + 1; startYear++)
        {
            var windowWeight = Enumerable
                .Range(startYear, windowSize)
                .Sum(year => yearlyWeight.GetValueOrDefault(year));

            if (windowWeight > bestWindowWeight)
            {
                bestWindowWeight = windowWeight;
                bestStartYear = startYear;
            }
        }

        var bestEndYear = Math.Min(bestStartYear + windowSize - 1, maxYear);

        // Get all games released within the player's strongest era.
        var peakEraGames = gameList
            .Where(g =>
            {
                var year = releaseDates[g]!.Value.Year;

                return year >= bestStartYear &&
                       year <= bestEndYear;
            })
            .ToList();

        var peakEraWeight = peakEraGames.Sum(GetWeight);

        if (peakEraWeight == 0) return 0;

        // Find where within the era the player's gaming is concentrated.
        // This allows the result to lean towards the earlier or later
        // end of the window rather than always using the midpoint.
        var weightedReleaseYear = peakEraGames
            .Sum(g =>
                releaseDates[g]!.Value.Year * GetWeight(g))
            / peakEraWeight;

        var windowLength = bestEndYear - bestStartYear;

        // Position within the window:
        // 0 = earliest year, 1 = latest year.
        var yearPosition = windowLength > 0
            ? (weightedReleaseYear - bestStartYear) / windowLength
            : 0.5;

        const double assumedStartAge = 13;
        const double assumedEndAge = 18;

        // Assume the player was aged 13-18 during this era.
        var estimatedAgeDuringEra = assumedStartAge + yearPosition * (assumedEndAge - assumedStartAge);

        var estimatedBirthYear = weightedReleaseYear - estimatedAgeDuringEra;

        return DateTime.UtcNow.Year - estimatedBirthYear;
    }

    private static DateOnly CalculatePlaytimeWeightedAverageReleaseDate(IEnumerable<EnrichedPlayerGame> games, Dictionary<EnrichedPlayerGame, DateOnly?> releaseDates)
    {
        var gamesWithPlaytime = games
            .Where(g =>
                releaseDates.TryGetValue(g, out var releaseDate) &&
                releaseDate.HasValue &&
                g.PlayerGame.PlayHours.HasValue &&
                g.PlayerGame.PlayHours.Value > 0)
            .ToList();

        var totalPlaytime = gamesWithPlaytime.Sum(g =>
            g.PlayerGame.PlayHours!.Value);

        if (totalPlaytime <= 0)
            return default;

        var weightedDayNumber = gamesWithPlaytime.Sum(g =>
            releaseDates[g]!.Value.DayNumber *
            g.PlayerGame.PlayHours!.Value);

        return DateOnly.FromDayNumber(
            (int)Math.Round(weightedDayNumber / totalPlaytime));
    }

    private static double? NormaliseReviewRating(double? rating, int? reviewCount)
    {
        if (!rating.HasValue || !reviewCount.HasValue) return null;

        const double averageRating = 70; // Adjust based on dataset
        const double minimumReviews = 50;

        var v = reviewCount.Value;
        var R = rating.Value;

        return (v / (v + minimumReviews) * R) + (minimumReviews / (v + minimumReviews) * averageRating);
    }
}