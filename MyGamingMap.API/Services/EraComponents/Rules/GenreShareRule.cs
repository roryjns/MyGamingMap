namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class GenreShareRule : ChartRule
{
    protected override string EntityKey => "genres";

    protected override IReadOnlyList<(string Label, int Value)> Slices(EraAnalytics era) =>
        era.IGDBAnalytics.GenreAnalytics
            .Where(g => g.GamesPlayed > 0)
            .Select(g => (g.Name, g.GamesPlayed))
            .ToList();

    protected override ComponentBasis Basis => ComponentBasis.GenresByGamesPlayed;

    protected override string Unit => "games";

    protected override string DescriptionFor(IReadOnlyList<(string Label, int Value)> slices)
    {
        var (Label, Value) = slices[0]; // already ordered descending
        return $"Your most played genre was {Label} ({Value} games).";
    }
}