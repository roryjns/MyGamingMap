namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

internal static class Quiz
{
    public const int OptionCount = 4;

    public static bool HasClearFirst(IReadOnlyList<double> values, double ratio = 1.5)
    {
        if (values.Count < 2) return false;
        var ordered = values.OrderByDescending(v => v).ToList();
        return ordered[0] >= ratio * ordered[1];
    }

    public static QuizComponent Build(
        string question,
        EnrichedPlayerGame correct,
        IEnumerable<EnrichedPlayerGame> distractors,
        int seed)
    {
        var options = new List<QuizOption>
        {
            new() { Label = correct.PlayerGame.Name, Game = correct },
        };
        options.AddRange(distractors.Select(d => new QuizOption
        {
            Label = d.PlayerGame.Name,
            Game = d,
        }));

        // Seeded Fisher–Yates: stable per era, random to the user.
        var rng = new Random(seed);
        for (var i = options.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (options[i], options[j]) = (options[j], options[i]);
        }

        var correctIndex = options.FindIndex(o => ReferenceEquals(o.Game, correct));

        return new QuizComponent
        {
            Question = question,
            Options = options,
            CorrectIndex = correctIndex,
        };
    }
}

public abstract class RankedQuizRule : IEraComponentRule
{
    protected abstract string EntityKey { get; }
    protected abstract string Question { get; }
    protected abstract IReadOnlyList<(string Label, EnrichedPlayerGame? Game, double Value)> Entries(EraAnalytics era);

    private const double ClearFirstRatio = 1.5;
    private const int MinEntries = 4;

    public string DataSource => $"quiz:{EntityKey}";

    public double Strength(EraAnalytics era)
    {
        var entries = Entries(era);
        if (entries.Count < 2) return 0;

        var ordered = entries.OrderByDescending(e => e.Value).ToList();
        var second = ordered[1].Value;
        if (second <= 0) return 1;

        var ratio = ordered[0].Value / second;   // e.g. 2.0 means top is 2× second
        const double target = 3.0;               // 3× second = "perfect" strength
        return Math.Clamp((ratio - ClearFirstRatio) / (target - ClearFirstRatio), 0, 1);
    }

    public bool IsEligible(EraAnalytics era)
    {
        var entries = Entries(era);
        if (entries.Count < MinEntries) return false;
        return Quiz.HasClearFirst(entries.Select(e => e.Value).ToList(), ClearFirstRatio);
    }

    public EraComponent Build(EraAnalytics era)
    {
        var ordered = Entries(era).OrderByDescending(e => e.Value).ToList();
        var (Label, Game, Value) = ordered[0];

        var distractors = ordered
            .Skip(1)
            .Take(Quiz.OptionCount - 1)
            .Where(d => d.Game is not null)
            .Select(d => d.Game!);

        return Quiz.Build(
            Question,
            correct: Game!,
            distractors,
            seed: HashCode.Combine(EntityKey, era.Start));
    }
}

public abstract class SpotTheOneQuizRule : IEraComponentRule
{
    protected abstract string EntityKey { get; }
    protected abstract string Question { get; }
    protected abstract IReadOnlyList<EnrichedPlayerGame> Accepted(EraAnalytics era);
    protected abstract bool NearMiss(EraAnalytics era, EnrichedPlayerGame g);
    protected abstract double Proximity(EnrichedPlayerGame g);

    private const int RequiredNearMisses = 3;

    public string DataSource => $"quiz:{EntityKey}";

    public double Strength(EraAnalytics era)
    {
        if (Accepted(era).Count != 1) return 0;
        return IEraComponentRule.CountStrength(NearMisses(era).Count, 6);
    }

    public bool IsEligible(EraAnalytics era)
    {
        if (Accepted(era).Count != 1) return false;
        return NearMisses(era).Count >= RequiredNearMisses;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var correct = Accepted(era)[0];
        var distractors = NearMisses(era)
            .OrderByDescending(Proximity)
            .Take(Quiz.OptionCount - 1);

        return Quiz.Build(
            Question,
            correct,
            distractors,
            seed: HashCode.Combine(EntityKey, era.Start));
    }

    private List<EnrichedPlayerGame> NearMisses(EraAnalytics era)
    {
        var accepted = Accepted(era).ToHashSet(new GameByIdentity());
        return [.. era.MergedGamesStarted
            .Where(g => !accepted.Contains(g))
            .Where(g => NearMiss(era, g))];
    }

    internal sealed class GameByIdentity : IEqualityComparer<EnrichedPlayerGame>
    {
        public bool Equals(EnrichedPlayerGame? x, EnrichedPlayerGame? y) =>
            x is not null && y is not null &&
            string.Equals(x.PlayerGame.Name, y.PlayerGame.Name, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode(EnrichedPlayerGame g) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(g.PlayerGame.Name);
    }
}