namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class TopFranchisesRule : CardinalityRule<FranchiseAnalytic>
{
    protected override string EntityKey => "franchises";

    protected override IReadOnlyList<FranchiseAnalytic> Entities(EraAnalytics era) =>
        era.IGDBAnalytics.FranchiseAnalytics;

    protected override EnrichedPlayerGame? RepresentativeGame(
        EraAnalytics era, FranchiseAnalytic f) =>
        IEraComponentRule.FindRepresentativeGame(era, franchise: f.Name);

    protected override ComponentBasis GetBasis(RankingMetric metric) => metric switch
    {
        RankingMetric.Hours       => ComponentBasis.FranchisesByHours,
        RankingMetric.GamesPlayed => ComponentBasis.FranchisesByGames,
        _ => throw new ArgumentOutOfRangeException(nameof(metric)),
    };

    protected override string DescriptionFor(RankingMetric metric) => metric switch
    {
        RankingMetric.Hours       => "The franchises whose games took the most time.",
        RankingMetric.GamesPlayed => "The franchises you sampled most.",
        _ => "Top franchises",
    };

    protected override string CalloutStatLabel(EraAnalytics era, FranchiseAnalytic d)
        => $"{d.HoursPlayed:F0} hours";
}