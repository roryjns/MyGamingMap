namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class TopAgeRatingsRule : IEraComponentRule
{
    private const double MinScore = 0.3;

    public string DataSource => "bar:age-ratings";

    public bool IsEligible(EraAnalytics era)
    {
        var (system, ordered) = BuildOrdered(era);

        if (system is null)
        {
            Log(era, "ineligible (no age ratings)");
            return false;
        }

        var nonZero = ordered.Where(o => o.Value > 0).ToList();
        if (nonZero.Count < 2)
        {
            Log(era, $"ineligible ({system}: < 2 distinct ratings)");
            return false;
        }

        var score = IEraComponentRule.ScoreMetric(nonZero, o => o.Value);
        if (score < MinScore)
        {
            Log(era, $"ineligible ({system}: score {score:F3} < {MinScore:F3})");
            return false;
        }

        Log(era, $"bar ({system}, score={score:F3})");
        return true;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var (system, ordered) = BuildOrdered(era);

        return new BarChartComponent
        {
            Basis = ComponentBasis.AgeRatingsByGamesPlayed,
            Description = Describe(system!, ordered),
            Entries = [.. ordered
                .Select(o => new BarChartEntry
                {
                    Label = o.Label,
                    Value = o.Value,
                    Unit = "games",
                })],
        };
    }

    private static (string? System, List<(string Label, int Value)> Ordered) BuildOrdered(
    EraAnalytics era)
    {
        var analytics = era.IGDBAnalytics?.AgeRatingAnalytics;
        if (analytics is null)
            return (null, []);

        var pegi = analytics.PEGIRatingAnalytics ?? [];
        var esrb = analytics.ESRBRatingAnalytics ?? [];

        var pegiTotal = pegi.Sum(a => a.GamesPlayed);
        var esrbTotal = esrb.Sum(a => a.GamesPlayed);

        if (pegiTotal == 0 && esrbTotal == 0)
            return (null, []);

        var usePegi = pegiTotal >= esrbTotal;
        var system = usePegi ? "PEGI" : "ESRB";
        var source = usePegi ? pegi : esrb;
        var order = usePegi ? AgeRatingOrder.Pegi : AgeRatingOrder.Esrb;

        var counts = source
            .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.GamesPlayed),
                          StringComparer.OrdinalIgnoreCase);

        var ordered = order
            .Select(label => (Label: label, Value: counts.GetValueOrDefault(label)))
            .ToList();

        return (system, ordered);
    }

    private static string Describe(string system, List<(string Label, int Value)> ordered)
    {
        var (Label, Value) = ordered.OrderByDescending(o => o.Value).First();
        return $"Most of your {system}-rated games were {Label}.";
    }

    private void Log(EraAnalytics era, string decision) =>
        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: {decision}");
}

internal static class AgeRatingOrder
{
    public static readonly string[] Pegi =
    [
        "3", "7", "12", "16", "18",
    ];

    public static readonly string[] Esrb =
    [
        "E", "E10\u002B", "T", "M",
    ];
}