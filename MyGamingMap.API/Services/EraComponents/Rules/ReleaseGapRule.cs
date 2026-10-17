namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public abstract class ReleaseGapRule : IEraComponentRule
{
    protected abstract string EntityKey { get; }
    protected abstract IReadOnlyList<ReleaseGapGame> Entities(EraAnalytics era);

    /// True if larger DaysAfterRelease ranks higher (long-after), false for soon-after.
    protected abstract bool LargerIsBetter { get; }

    protected abstract string DescriptionFor(bool countdown);

    private const int CalloutMax = 4;
    private const int CountdownMax = 10;
    private const double MinScore = 0.1;

    public string DataSource => $"countdown:{EntityKey}";

    public bool IsEligible(EraAnalytics era)
    {
        var all = Entities(era);

        if (all.Count == 0)
        {
            Log(era, all.Count, "ineligible (no entries)");
            return false;
        }

        Log(era, all.Count, all.Count <= CalloutMax ? "callout" : "countdown");
        return true;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var all = Entities(era);
        return all.Count <= CalloutMax
            ? BuildCallout(era, all)
            : BuildCountdown(era, all);
    }

    private CalloutComponent BuildCallout(EraAnalytics era, IReadOnlyList<ReleaseGapGame> all)
    {
        var entries = Ordered(all)
            .Select(g => new CalloutEntry
            {
                Label = g.Game.PlayerGame.Name,
                Game = g.Game,
                Value = g.DaysAfterRelease,
                Unit = "days after release",
            })
            .ToList();

        return new CalloutComponent
        {
            Basis = ComponentBasis.DaysAfterRelease,
            Description = DescriptionFor(countdown: false),
            Entries = entries,
        };
    }

    private CountdownComponent BuildCountdown(EraAnalytics era, IReadOnlyList<ReleaseGapGame> all)
    {
        var entries = Ordered(all)
            .Take(CountdownMax)
            .Select((g, idx) => new CountdownEntry
            {
                Rank = idx + 1,
                Label = g.Game.PlayerGame.Name,
                Game = g.Game,
                Value = g.DaysAfterRelease,
                Unit = "days after release",
            })
            .ToList();

        return new CountdownComponent
        {
            Basis = ComponentBasis.DaysAfterRelease,
            Description = DescriptionFor(countdown: true),
            Entries = entries,
        };
    }

    private IEnumerable<ReleaseGapGame> Ordered(IReadOnlyList<ReleaseGapGame> all) =>
        LargerIsBetter
            ? all.OrderByDescending(g => g.DaysAfterRelease)
            : all.OrderBy(g => g.DaysAfterRelease);

    private void Log(EraAnalytics era, int count, string decision) =>
        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: " +
            $"count={count} -> {decision}");
}

public sealed class PlayedSoonAfterReleaseRule : ReleaseGapRule
{
    protected override string EntityKey => "played soon after release";

    protected override IReadOnlyList<ReleaseGapGame> Entities(EraAnalytics era) =>
        era.IGDBAnalytics.ReleaseDateAnalytics.PlayedSoonAfterRelease;

    protected override bool LargerIsBetter => false;

    protected override string DescriptionFor(bool countdown) => countdown
        ? "The shortest gaps from release to first play."
        : "How soon after release you played these.";
}

public sealed class PlayedLongAfterReleaseRule : ReleaseGapRule
{
    protected override string EntityKey => "played long after release";

    protected override IReadOnlyList<ReleaseGapGame> Entities(EraAnalytics era) =>
        era.IGDBAnalytics.ReleaseDateAnalytics.PlayedLongAfterRelease;

    protected override bool LargerIsBetter => true;

    protected override string DescriptionFor(bool countdown) => countdown
        ? "The longest gaps from release to first play."
        : "How long after release you finally played these.";
}