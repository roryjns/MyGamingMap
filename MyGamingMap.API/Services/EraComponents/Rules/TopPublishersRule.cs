namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class TopPublishersRule : CardinalityRule<CompanyAnalytic>
{
    protected override string EntityKey => "publishers";

    protected override IReadOnlyList<CompanyAnalytic> Entities(EraAnalytics era) =>
        era.IGDBAnalytics.PublisherAnalytics;

    protected override EnrichedPlayerGame? RepresentativeGame(
        EraAnalytics era, CompanyAnalytic p) =>
        IEraComponentRule.FindRepresentativeGame(era, publisher: p.Name);

    protected override ComponentBasis GetBasis(RankingMetric metric) => metric switch
    {
        RankingMetric.Hours => ComponentBasis.PublishersByHours,
        RankingMetric.GamesPlayed => ComponentBasis.PublishersByGames,
        _ => throw new ArgumentOutOfRangeException(nameof(metric)),
    };

    protected override string DescriptionFor(RankingMetric metric) => metric switch
    {
        RankingMetric.Hours => "The publishers whose games took the most time.",
        RankingMetric.GamesPlayed => "The publishers you played the most games from.",
        _ => "Top publishers",
    };

    protected override string CalloutStatLabel(EraAnalytics era, CompanyAnalytic d)
        => $"{d.HoursPlayed:F0} hours";
}