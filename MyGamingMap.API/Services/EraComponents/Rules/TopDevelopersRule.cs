namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class TopDevelopersRule : CardinalityRule<CompanyAnalytic>
{
    protected override string EntityKey => "developers";

    protected override IReadOnlyList<CompanyAnalytic> Entities(EraAnalytics era) =>
        era.IGDBAnalytics.DeveloperAnalytics;

    protected override EnrichedPlayerGame? RepresentativeGame(
        EraAnalytics era, CompanyAnalytic d) =>
        IEraComponentRule.FindRepresentativeGame(era, developer: d.Name);

    protected override ComponentBasis GetBasis(RankingMetric metric) => metric switch
    {
        RankingMetric.Hours => ComponentBasis.DevelopersByHours,
        RankingMetric.GamesPlayed => ComponentBasis.DevelopersByGames,
        _ => throw new ArgumentOutOfRangeException(nameof(metric)),
    };

    protected override string DescriptionFor(RankingMetric metric) => metric switch
    {
        RankingMetric.Hours => "The developers behind your most played games.",
        RankingMetric.GamesPlayed => "The developers you returned to most.",
        _ => "Top developers",
    };

    protected override string CalloutStatLabel(EraAnalytics era, CompanyAnalytic d)
        => $"{d.HoursPlayed:F0} hours";
}