namespace MyGamingMap.API.Services.EraComponents;

using MyGamingMap.API.Models.DTOs;
using MyGamingMap.API.Services.EraComponents.Rules;

public static class EraComponentSelector
{
    private static readonly IReadOnlyList<IEraComponentRule> Rules =
    [
        // Quizzes — must come before their callout counterparts so ordering
        // (quiz before callout) is preserved after any dedup pass.
        new SingleDayQuizRule(),
        new AbandonedQuizRule(),
        new CompletedQuizRule(),

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
        new TopPlatformsRule(),
        new TopGameModesRule(),
        new TopGenresRule(),
        new TopThemesRule(),
        new TopAgeRatingsRule(),
        new TopReleaseYearsRule(),
        new TimelineRule(),
    ];

    // Per-era selection (unchanged semantics, but now also yields strength).
    public static List<EraComponent> Select(EraAnalytics era) =>
        [.. Rules
            .Where(r => r.IsEligible(era))
            .DistinctBy(r => r.DataSource)
            .Select(r => r.Build(era))];

    /// `candidates` is (Era, built component, strength) per era.
    public static List<(Era Era, EraComponent Component)> SelectDiverse(
    IReadOnlyList<(Era Era, EraAnalytics Analytics)> eras,
    int maxTotal = 36, int perEraMin = 3, // enforces 6-36 components total
    int minTypes = 6, // must include x of the 7 component types
    int maxPerTypePerEra = 2) // e.g. only x countdowns per era
    {
        // ── Stage A: build the candidate pool ────────────────────────────────
        // perEraMax is derived from maxTotal: split the budget evenly across
        // eras, clamped to [perEraMin, 8] so every era gets a shot but none
        // dominates.
        var perEraMax = Math.Clamp(
            (int)Math.Ceiling(maxTotal / (double)Math.Max(eras.Count, 1)),
            perEraMin,
            8);

        var pool = new List<(Era Era, IEraComponentRule Rule, EraComponent C, double Strength, string Type)>();

        foreach (var (era, analytics) in eras)
        {
            var picked = Rules
                .Where(r => r.IsEligible(analytics))
                .DistinctBy(r => r.DataSource)
                .Select(r => (Rule: r, Strength: r.Strength(analytics), C: r.Build(analytics)))
                .OrderByDescending(x => x.Strength)
                .Take(perEraMax)
                .ToList();

            foreach (var (Rule, Strength, C) in picked)
                pool.Add((era, Rule, C, Strength, TypeKey(C)));
        }

        // ── Stage B: greedy pick ─────────────────────────────────────────────
        // Primary: strength. Secondary: type rarity (encourages cross-type
        // variety). Hard caps: maxTotal, maxPerRule, maxPerTypePerEra.
        var selected = new List<(Era Era, IEraComponentRule Rule, EraComponent C, double Strength)>();
        var typeCounts = new Dictionary<string, int>();
        var ruleCounts = new Dictionary<string, int>();
        var eraCounts = new Dictionary<DateTime, int>();

        while (selected.Count < maxTotal && pool.Count > 0)
        {
            var best = pool
                .Where(p => CountForEraType(selected, p.Era.Start, p.Type) < maxPerTypePerEra)
                .Where(p => eraCounts.GetValueOrDefault(p.Era.Start) < perEraMax)
                .OrderByDescending(p => p.Strength / (1 + typeCounts.GetValueOrDefault(p.Type)))
                .FirstOrDefault();

            if (best.Rule is null) break;

            var ruleName = best.Rule.GetType().Name;
            selected.Add((best.Era, best.Rule, best.C, best.Strength));
            pool.Remove(best);
            typeCounts[best.Type] = typeCounts.GetValueOrDefault(best.Type) + 1;
            ruleCounts[ruleName] = ruleCounts.GetValueOrDefault(ruleName) + 1;
            eraCounts[best.Era.Start] = eraCounts.GetValueOrDefault(best.Era.Start) + 1;
        }

        // ── Min types: force-include missing types ───────────────────────────
        // If the greedy produced fewer than minTypes distinct types, pull the
        // strongest missing type from the pool. Respects all caps above so it
        // can't undo the greedy's balance.
        while (typeCounts.Count < minTypes && pool.Count > 0)
        {
            var missing = pool
                .Where(p => CountForEraType(selected, p.Era.Start, p.Type) < maxPerTypePerEra)
                .Where(p => eraCounts.GetValueOrDefault(p.Era.Start) < perEraMax)
                .Where(p => !typeCounts.ContainsKey(p.Type))
                .OrderByDescending(p => p.Strength)
                .FirstOrDefault();

            if (missing.Rule is null) break;

            var ruleName = missing.Rule.GetType().Name;
            selected.Add((missing.Era, missing.Rule, missing.C, missing.Strength));
            pool.Remove(missing);
            typeCounts[missing.Type] = typeCounts.GetValueOrDefault(missing.Type) + 1;
            ruleCounts[ruleName] = ruleCounts.GetValueOrDefault(ruleName) + 1;
            eraCounts[missing.Era.Start] = eraCounts.GetValueOrDefault(missing.Era.Start) + 1;
        }

        // ── Per-era floor: soft backfill ─────────────────────────────────────
        // Any era with fewer than perEraMin components gets backfilled from its
        // own remaining pool, but backfill respects maxPerRule and maxPerTypePerEra
        // so it can't reintroduce a flood. perEraMax is NOT enforced during
        // backfill — the floor takes precedence over the ceiling for a sparse era.
        foreach (var (era, _) in eras)
        {
            var have = selected.Count(s => s.Era.Start == era.Start);
            if (have >= perEraMin) continue;

            var backfill = pool
                .Where(p => p.Era.Start == era.Start)
                .Where(p => CountForEraType(selected, p.Era.Start, p.Type) < maxPerTypePerEra)
                .OrderByDescending(p => p.Strength)
                .Take(perEraMin - have)
                .ToList();

            foreach (var p in backfill)
            {
                var ruleName = p.Rule.GetType().Name;
                selected.Add((p.Era, p.Rule, p.C, p.Strength));
                pool.Remove(p);
                typeCounts[p.Type] = typeCounts.GetValueOrDefault(p.Type) + 1;
                ruleCounts[ruleName] = ruleCounts.GetValueOrDefault(ruleName) + 1;
            }
        }

        // ── Logging ──────────────────────────────────────────────────────────
        Console.WriteLine($"Per Era Max: {perEraMax}, Per Era Min: {perEraMin}");
        Console.WriteLine();
        Console.WriteLine($"── Chosen ({selected.Count}) ─────────────────────────────────────");

        DateTime? lastEra = null;
        foreach (var (era, rule, c, strength) in selected
            .OrderBy(s => s.Era.Start)
            .ThenByDescending(s => s.Strength))
        {
            if (lastEra is not null && era.Start != lastEra)
                Console.WriteLine();

            Console.WriteLine(
                $"  {era.Start:yyyy-MM}  {strength,5:F2}  {rule.GetType().Name,-28}  {TypeKey(c)}");

            lastEra = era.Start;
        }
        Console.WriteLine();

        return [.. selected.Select(s => (s.Era, s.C))];
    }

    private static int CountForEraType(
    List<(Era Era, IEraComponentRule Rule, EraComponent C, double Strength)> selected,
    DateTime eraStart, string type) =>
    selected.Count(s => s.Era.Start == eraStart && TypeKey(s.C) == type);

    private static string TypeKey(EraComponent c) => c switch
    {
        QuizComponent => "quiz",
        CalloutComponent => "callout",
        CountdownComponent => "countdown",
        GridComponent => "grid",
        DonutChartComponent => "donut",
        BarChartComponent => "bar",
        TimelineComponent => "timeline",
        _ => c.GetType().Name,
    };
}