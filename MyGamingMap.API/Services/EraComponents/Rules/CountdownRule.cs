namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public enum RankingMetric
{
    Hours,
    GamesPlayed,
}

/// Non-generic marker base. Useful for registration and any shared
/// non-entity-specific behaviour.
public abstract class CountdownRule : IEraComponentRule
{
    protected abstract string EntityKey { get; }

    // DataSource no longer depends on a per-instance Metric — the
    // generic base picks the metric dynamically per era.
    public string DataSource => $"countdown:{EntityKey}";

    public abstract bool IsEligible(EraAnalytics era);
    public abstract EraComponent Build(EraAnalytics era);
}

/// Generic base that owns the pipeline. Subclasses declare
/// entity-specific data only.
public abstract class CountdownRule<T> : CountdownRule
    where T : CategoryAnalytic
{
    protected abstract IReadOnlyList<T> Entities(EraAnalytics era);
    protected abstract EnrichedPlayerGame? RepresentativeGame(EraAnalytics era, T entity);
    protected abstract ComponentBasis GetBasis(RankingMetric metric);
    protected abstract string DescriptionFor(RankingMetric metric);

    private const int MinEntries = 5;
    private const int MaxEntries = 10;
    private const double MinScore = 0.15;
    private static int CountdownSize(int available) =>
    available >= MaxEntries ? MaxEntries : MinEntries;

    public sealed override bool IsEligible(EraAnalytics era)
    {
        var all = Entities(era);

        if (all.Count < MinEntries)
        {
            Log(era, null, null,
                $"ineligible (only {all.Count} {EntityKey}, need {MinEntries})");
            return false;
        }

        var (hours, games) = Scores(all);
        var metric = hours >= games
            ? RankingMetric.Hours
            : RankingMetric.GamesPlayed;
        var unit = metric == RankingMetric.Hours ? "hours" : "games played";
        var best = Math.Max(hours, games);

        if (best < MinScore)
        {
            Log(era, hours, games,
                $"ineligible (best {best:F3} < {MinScore:F3}, unit={unit})");
            return false;
        }

        Log(era, hours, games, $"countdown, unit={unit}");
        return true;
    }

    public sealed override EraComponent Build(EraAnalytics era)
    {
        var all = Entities(era);
        var (hours, games) = Scores(all);

        var metric = hours >= games ? RankingMetric.Hours : RankingMetric.GamesPlayed;
        var unit = metric == RankingMetric.Hours ? "hours" : "games played";

        // No log here — IsEligible already logged the decision.

        var key = metric == RankingMetric.Hours
            ? (Func<T, double>)(e => e.HoursPlayed)
            : e => e.GamesPlayed;

        var total = all.Sum(key);

        var size = CountdownSize(all.Count);

        var entries = all
            .OrderByDescending(key)
            .Take(size)
            .Select((e, idx) =>
            {
                var value = key(e);
                return new CountdownEntry
                {
                    Rank = idx + 1,
                    Label = e.Name,
                    Game = RepresentativeGame(era, e),
                    Value = value,
                    Unit = unit,
                    Share = total > 0 ? value / total : null,
                };
            })
            .ToList();

        return new CountdownComponent
        {
            Basis = GetBasis(metric),
            Description = DescriptionFor(metric),
            Entries = entries,
        };
    }

    private static (double Hours, double Games) Scores(IReadOnlyList<T> all) =>
        (IEraComponentRule.ScoreMetric(all, e => e.HoursPlayed),
         IEraComponentRule.ScoreMetric(all, e => e.GamesPlayed));

    private void Log(EraAnalytics era, double? hours, double? games, string decision)
    {
        var h = hours is null ? "n/a" : hours.Value.ToString("F3");
        var g = games is null ? "n/a" : games.Value.ToString("F3");

        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
            $"hours={h}, gamesPlayed={g} -> {decision}");
    }
}