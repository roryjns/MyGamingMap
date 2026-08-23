using MyGamingMap.API.Models.DTOs;

namespace MyGamingMap.API.Services;

public class PSNAnalyticsService
{
    private const int MostPlayedGamesCount = 10;
    private const int LongestGamesCount = 10;
    private const int SingleDayGamesCount = 10;
    private const int LongestDroughtsCount = 3;
    private const int GamingDroughtThresholdDays = 14;
    private const int minimumDaysSincePlayedForAbandoned = 60;
    private const double minimumHoursPlayedForAbandoned = 3;
    private const int maximumTrophyProgressForAbandoned = 20;
    private const int AbandonedGamesCount = 10;

    public PSNAnalytics CalculatePSNAnalytics(List<EnrichedPlayerGame> games)
    {
        return new PSNAnalytics
        {
            ActivityAnalytics = CalculateActivityAnalytics(games),
            TrophyAnalytics = CalculateTrophyAnalytics(games)
        };
    }

    public ActivityAnalytics CalculateActivityAnalytics(List<EnrichedPlayerGame> games)
    {
        var gamesWithPlaytime = games
            .Where(g => g.PlayerGame.PlayHours.HasValue && g.PlayerGame.PlayCount.HasValue)
            .ToList();

        var playtimeDates = gamesWithPlaytime
            .SelectMany(g => new DateTime?[]
            {
                g.PlayerGame.FirstPlayed,
                g.PlayerGame.LastPlayed,
                g.PlayerGame.TrophyData
            .Where(t => t.LastTrophyEarned.HasValue)
            .Select(t => t.LastTrophyEarned)
            .FirstOrDefault()
            })
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();

        var playtimeSpanDays = 0.0;

        if (playtimeDates.Count > 0)
        {
            var firstPlaytimeDate = playtimeDates.Min();
            var lastPlaytimeDate = playtimeDates.Max();
            playtimeSpanDays = (lastPlaytimeDate - firstPlaytimeDate).TotalDays;
        }

        var gamesWithDates = games
            .Select(g => new
            {
                Game = g,
                FirstDay = GetFirstActivityDate(g.PlayerGame),
                LastDay = GetLastActivityDate(g.PlayerGame)
            })
            .Where(x => x.FirstDay.HasValue && x.LastDay.HasValue)
            .Select(x => new GameActivityPeriod
            {
                Game = x.Game,
                FirstDay = x.FirstDay!.Value,
                LastDay = x.LastDay!.Value
            })
            .ToList();

        var totalHoursPlayed = gamesWithPlaytime.Sum(g => g.PlayerGame.PlayHours!.Value);
        var totalSessionsPlayed = gamesWithPlaytime.Sum(g => g.PlayerGame.PlayCount!.Value);

        var result = new ActivityAnalytics
        {
            TotalGamesPlayed = games.Count,
            TotalHoursPlayed = totalHoursPlayed,
            TotalSessionsPlayed = totalSessionsPlayed,

            AverageHoursPerGame =
                CalculateAverage(
                    gamesWithPlaytime.Select(g => g.PlayerGame.PlayHours!.Value)),

            MedianHoursPerGame =
                CalculateMedian(
                    gamesWithPlaytime.Select(g => g.PlayerGame.PlayHours!.Value)),

            AverageSessionsPerGame =
                CalculateAverage(
                    gamesWithPlaytime.Select(g => (double)g.PlayerGame.PlayCount!.Value)),

            MedianSessionsPerGame =
                CalculateMedian(
                    gamesWithPlaytime.Select(g => (double)g.PlayerGame.PlayCount!.Value)),

            AverageSessionLength =
                totalSessionsPlayed > 0
                    ? totalHoursPlayed / totalSessionsPlayed
                    : 0
        };

        if (gamesWithDates.Count > 0)
        {
            result.FirstDay = gamesWithDates.Min(x => x.FirstDay!);
            result.LastDay = gamesWithDates.Max(x => x.LastDay!);

            result.ActivitySpanDays =
                result.FirstDay.HasValue && result.LastDay.HasValue
                    ? (result.LastDay.Value.Date - result.FirstDay.Value.Date).Days + 1
                    : 0;
        }

        var gameSpans = gamesWithDates
            .Select(x => new
            {
                x.Game,
                SpanDays = (x.LastDay! - x.FirstDay!).Days + 1
            })
            .ToList();

        result.AverageGameSpanDays = CalculateAverage(gameSpans.Select(x => (double)x.SpanDays));
        result.MedianGameSpanDays = CalculateMedian(gameSpans.Select(x => (double)x.SpanDays));

        result.AverageHoursPerDay =
            playtimeSpanDays > 0
                ? result.TotalHoursPlayed / playtimeSpanDays
                : result.TotalHoursPlayed;

        result.LongestRunningGames = [.. gameSpans
            .Where(x => x.Game.PlayerGame.ConceptId.HasValue)
            .GroupBy(x => x.Game.PlayerGame.ConceptId!.Value)
            .Select(group =>
            {
                var first = group.Min(x => x.Game.PlayerGame.FirstPlayed!.Value);
                var last = group.Max(x => x.Game.PlayerGame.LastPlayed!.Value);

                return new
                {
                    group
                        .OrderByDescending(x => x.SpanDays)
                        .First()
                        .Game,
                    SpanDays = (last - first).TotalDays
                };
            })
            .OrderByDescending(x => x.SpanDays)
            .Take(LongestGamesCount)
            .Select(x => x.Game)];

        result.SingleDayGames = [.. games
            .Where(g =>
                g.PlayerGame.FirstPlayed != null &&
                g.PlayerGame.LastPlayed != null &&
                g.PlayerGame.LastPlayed - g.PlayerGame.FirstPlayed <= TimeSpan.FromHours(24) &&
                g.PlayerGame.PlayHours.HasValue &&
                (
                    g.PlayerGame.PlayHours.Value >= minimumHoursPlayedForAbandoned ||
                    (g.PlayerGame.PlayHours.Value >= 1.0 &&
                    g.PlayerGame.TrophyData.Any(t => t.Progress >= maximumTrophyProgressForAbandoned))
                )
            )
            .OrderByDescending(g =>
                g.PlayerGame.TrophyData.Count != 0
                    ? g.PlayerGame.TrophyData.Max(t => t.Progress)
                    : 0
            )
            .ThenByDescending(g => g.PlayerGame.PlayHours ?? 0)
            .Take(SingleDayGamesCount)];

        var droughts = CalculateNewGameDroughts(games);

        if (droughts.Count > 0)
        {
            result.NewGameDroughts = [.. droughts
                .OrderByDescending(d => d.DurationDays)
                .Take(LongestDroughtsCount)];
        }

        result.GamesStartedPerYear = [.. games
                .Where(g => g.PlayerGame.FirstPlayed.HasValue)
                .GroupBy(g => g.PlayerGame.FirstPlayed!.Value.Year)
                .OrderBy(g => g.Key)
                .Select(g => new GamesStartedByYear
                {
                    Year = g.Key,
                    GamesStarted = g.Count()
                })];

        var cutoffDate = DateTime.UtcNow.AddDays(-minimumDaysSincePlayedForAbandoned);

        result.MostAbandonedGames = [.. games
            .Where(g =>
            g.PlayerGame.LastPlayed.HasValue &&
            g.PlayerGame.LastPlayed.Value < cutoffDate &&
            g.PlayerGame.PlayHours.HasValue &&
            g.PlayerGame.PlayHours.Value >= minimumHoursPlayedForAbandoned &&
            g.PlayerGame.TrophyData != null &&
            g.PlayerGame.TrophyData.Any(t => t.Progress > 0))
            .Select(g =>
            {
                var trophyProgress = g.PlayerGame.TrophyData
                    .Where(t => t.Progress > 0)
                    .Max(t => t.Progress);

                return new AbandonedGame
                {
                    Game = g,
                    DaysSinceLastPlayed =
                        (DateTime.UtcNow - g.PlayerGame.LastPlayed!.Value).Days,
                    TrophyProgress = trophyProgress
                };
            })
            .Where(x => x.TrophyProgress < maximumTrophyProgressForAbandoned)
            .OrderByDescending(x => x.Game.PlayerGame.PlayHours)
            .Take(AbandonedGamesCount)
            .ToList()];

        result.MostPlayedGames = [.. games
            .Where(g => g.PlayerGame.ConceptId.HasValue && g.PlayerGame.PlayHours.HasValue)
            .GroupBy(g => g.PlayerGame.ConceptId!.Value)
            .Select(group => new MostPlayedGame
            {
                Game = group
                    .OrderByDescending(g => g.PlayerGame.PlayHours!.Value)
                    .First(),

                HoursPlayed = group.Sum(g => g.PlayerGame.PlayHours!.Value),

                SessionsPlayed = group.Sum(g => g.PlayerGame.PlayCount!.Value),

                PercentageOfTotalPlaytime = group.Sum(g => g.PlayerGame.PlayHours!.Value / totalHoursPlayed) * 100
            })
            .OrderByDescending(x => x.HoursPlayed)
            .Take(MostPlayedGamesCount)];

        foreach (var game in result.MostPlayedGames)
        {
            game.PercentageOfTotalPlaytime =
                result.TotalHoursPlayed > 0
                    ? game.HoursPlayed / result.TotalHoursPlayed * 100
                    : 0;
        }

        result.PS3GamesPlayed = games.Count(
            g => string.Equals(
                g.PlayerGame.Platform,
                "PS3",
                StringComparison.OrdinalIgnoreCase)
        );

        result.PSVitaGamesPlayed = games.Count(
            g => string.Equals(
                g.PlayerGame.Platform,
                "PSVita",
                StringComparison.OrdinalIgnoreCase)
        );

        result.PS4 = CalculatePlatformPlaytime(
            games,
            "PS4"
        );

        result.PS5 = CalculatePlatformPlaytime(
            games,
            "PS5"
        );

        return result;
    }

    private static PlatformPlaytime CalculatePlatformPlaytime(List<EnrichedPlayerGame> games, string platform)
    {
        var platformGames = games
            .Where(g => string.Equals(
                g.PlayerGame.Platform,
                platform,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        var gamesWithPlaytime = platformGames
            .Where(g => g.PlayerGame.PlayHours.HasValue && g.PlayerGame.PlayCount.HasValue)
            .ToList();

        var gamesWithDates = platformGames
            .Select(g => new
            {
                Game = g,
                FirstDay = GetFirstActivityDate(g.PlayerGame),
                LastDay = GetLastActivityDate(g.PlayerGame)
            })
            .Where(x => x.FirstDay.HasValue && x.LastDay.HasValue)
            .ToList();

        var hoursPlayed = gamesWithPlaytime.Sum(g => g.PlayerGame.PlayHours!.Value);
        var totalSessionsPlayed = gamesWithPlaytime.Sum(g => g.PlayerGame.PlayCount!.Value);

        var result = new PlatformPlaytime
        {
            GamesPlayed = platformGames.Count,
            HoursPlayed = hoursPlayed,
            SessionsPlayed = totalSessionsPlayed,
            AverageHoursPerGame = CalculateAverage(gamesWithPlaytime.Select(g => g.PlayerGame.PlayHours!.Value)),
            MedianHoursPerGame = CalculateMedian(gamesWithPlaytime.Select(g => g.PlayerGame.PlayHours!.Value)),
            AverageSessionsPerGame = CalculateAverage(gamesWithPlaytime.Select(g => (double)g.PlayerGame.PlayCount!.Value)),
            MedianSessionsPerGame = CalculateMedian(gamesWithPlaytime.Select(g => (double)g.PlayerGame.PlayCount!.Value)),
            AverageSessionLength = hoursPlayed / totalSessionsPlayed
        };

        if (gamesWithDates.Count > 0)
        {
            result.FirstDay = gamesWithDates.Min(x => x.FirstDay!.Value);
            result.LastDay = gamesWithDates.Max(x => x.LastDay!.Value);
        }

        return result;
    }

    private static List<Drought> CalculateNewGameDroughts(IEnumerable<EnrichedPlayerGame> games)
    {
        var orderedGames = games
            .Where(g =>
                g.PlayerGame.FirstPlayed.HasValue &&
                g.PlayerGame.LastPlayed.HasValue)
            .OrderBy(g => g.PlayerGame.FirstPlayed!.Value)
            .ToList();

        var droughts = new List<Drought>();

        for (int i = 1; i < orderedGames.Count; i++)
        {
            var previous = orderedGames[i - 1];
            var current = orderedGames[i];

            var gapDays =
                (current.PlayerGame.FirstPlayed!.Value -
                 previous.PlayerGame.LastPlayed!.Value)
                .Days - 1;

            if (gapDays >= GamingDroughtThresholdDays)
            {
                droughts.Add(new Drought
                {
                    DurationDays = gapDays,
                    LastGameBefore = previous,
                    StartDate = previous.PlayerGame.LastPlayed!.Value.Date.AddDays(1),
                    EndDate = current.PlayerGame.FirstPlayed!.Value.Date,
                    FirstGameAfter = current,
                });
            }
        }

        return droughts;
    }

    private static DateTime? GetFirstActivityDate(PlayerGame game)
    {
        var dates = new List<DateTime>();

        if (game.FirstPlayed != null) dates.Add((DateTime)game.FirstPlayed);

        foreach (var trophySet in game.TrophyData)
        {
            if (trophySet.LastTrophyEarned.HasValue) dates.Add(trophySet.LastTrophyEarned.Value);
        }

        return dates.Count > 0 ? dates.Min() : null;
    }

    private static DateTime? GetLastActivityDate(PlayerGame game)
    {
        var dates = new List<DateTime>();

        if (game.LastPlayed.HasValue) dates.Add(game.LastPlayed.Value);

        foreach (var trophySet in game.TrophyData)
        {
            if (trophySet.LastTrophyEarned.HasValue) dates.Add(trophySet.LastTrophyEarned.Value);
        }

        return dates.Count > 0 ? dates.Max() : null;
    }

    public TrophyAnalytics CalculateTrophyAnalytics(IEnumerable<EnrichedPlayerGame> games)
    {
        var trophyGames = games
            .SelectMany(g => g.PlayerGame.TrophyData.Select(t => new TrophyGame
            {
                Game = g,
                Trophy = t
            }))
            .Where(x => x.Trophy.Progress > 0)
            .ToList();

        var result = new TrophyAnalytics();

        if (trophyGames.Count == 0) return result;

        result.TotalCompletion = TrophyAnalyticsHelper.CalculateTotalCompletion(trophyGames.Select(x => x.Trophy));

        var progress = trophyGames
            .Select(x => (double)x.Trophy.Progress)
            .ToList();

        result.AverageCompletion = CalculateAverage(progress);

        result.GamesCompleted =
            trophyGames
                .Where(x => x.Trophy.Progress >= 100)
                .Select(TrophyAnalyticsHelper.GetGameKey)
                .Count();

        result.PlatinumsEarned =
            trophyGames.Count(x =>
                x.Trophy.EarnedTrophies.Platinum >= 1);

        result.PlatinumsAvailable =
            trophyGames.Count(x =>
                x.Trophy.DefinedTrophies.Platinum > 0);

        result.PlatinumRate =
            result.PlatinumsAvailable > 0
                ? (double)result.PlatinumsEarned /
                  result.PlatinumsAvailable * 100
                : 0;

        result.PS3 = CalculatePlatformTrophies(trophyGames, "PS3");
        result.PSVita = CalculatePlatformTrophies(trophyGames, "PSVita");
        result.PS4 = CalculatePlatformTrophies(trophyGames, "PS4");
        result.PS5 = CalculatePlatformTrophies(trophyGames, "PS5");

        return result;
    }

    private static PlatFormTrophies CalculatePlatformTrophies(IEnumerable<TrophyGame> trophyGames, string platform)
    {
        var games = trophyGames
            .Where(x => x.Trophy.Platform == platform)
            .ToList();

        if (games.Count == 0) return new PlatFormTrophies();

        var progress = games
            .Select(x => (double)x.Trophy.Progress)
            .Where(x => x > 0)
            .ToList();

        var completedGames = games
            .Where(x => x.Trophy.Progress >= 100)
            .Select(TrophyAnalyticsHelper.GetGameKey)
            .Count();

        var platinumsEarned = games
            .Count(x =>
                x.Trophy.EarnedTrophies.Platinum >= 1);

        var platinumsAvailable = games
            .Count(x =>
                x.Trophy.DefinedTrophies.Platinum > 0);

        return new PlatFormTrophies
        {
            TotalCompletion = TrophyAnalyticsHelper.CalculateTotalCompletion(trophyGames.Select(x => x.Trophy)),
            AverageCompletion = CalculateAverage(progress),
            GamesCompleted = completedGames,
            PlatinumsEarned = platinumsEarned,
            PlatinumsAvailable = platinumsAvailable,

            PlatinumRate =
                platinumsAvailable > 0
                    ? (double)platinumsEarned /
                      platinumsAvailable * 100
                    : 0
        };
    }
    
    private static double CalculateAverage(IEnumerable<double> values)
    {
        var array = values.ToArray();
        return array.Length == 0 ? 0 : array.Average();
    }

    private static double CalculateMedian(IEnumerable<double> values)
    {
        var array = values
            .OrderBy(x => x)
            .ToArray();

        if (array.Length == 0) return 0;

        int middle = array.Length / 2;

        if (array.Length % 2 == 1) return array[middle];

        return (array[middle - 1] + array[middle]) / 2.0;
    }

    private sealed class GameActivityPeriod
    {
        public required EnrichedPlayerGame Game { get; set; }
        public required DateTime FirstDay { get; set; }
        public required DateTime LastDay { get; set; }
    }
}