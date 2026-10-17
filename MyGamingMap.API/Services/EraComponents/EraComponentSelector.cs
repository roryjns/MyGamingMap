namespace MyGamingMap.API.Services.EraComponents;

using MyGamingMap.API.Models.DTOs;
using MyGamingMap.API.Services.EraComponents.Rules;

public static class EraComponentSelector
{
    private static readonly IReadOnlyList<IEraComponentRule> Rules =
    [
        new TopGamesRule(),
        new TopAbandonedGamesRule(),
        new TopDevelopersRule(),
        new TopPublishersRule(),
        new TopFranchisesRule(),
        new LongTailStartsRule(),
        new LongTailEndsRule(),
        new PlayedSoonAfterReleaseRule(),
        new PlayedLongAfterReleaseRule(),
        new NewGameDroughtRule(),
        new CompletedGamesRule(),
        new SingleDayGamesRule(),
        new PlatformShareRule(),
        new GameModeShareRule(),
        new GenreShareRule(),
        new ThemeShareRule(),
        new TopAgeRatingsRule(),
        new TopReleaseYearsRule(),
    ];

    public static List<EraComponent> Select(EraAnalytics era)
    {
        return [.. Rules
            .Where(r => r.IsEligible(era))
            .DistinctBy(r => r.DataSource)
            .Select(r => r.Build(era))];
    }
}