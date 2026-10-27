namespace MyGamingMap.API.Services.EraSummaries;

using Microsoft.Extensions.AI;

public sealed class LlmEraSummaryService(IChatClient client, ILogger<LlmEraSummaryService> logger) : IEraSummaryService
{
    private readonly IChatClient _client = client;
    private readonly ILogger<LlmEraSummaryService> _logger = logger;

    public async Task<IReadOnlyList<(string Label, string Summary)?>> SummariseAllAsync(
        IReadOnlyList<EraFacts> eras, CancellationToken ct = default)
    {
        if (eras.Count == 0) return [];

        var userPrompt = EraFactsBuilder.BuildBatchPrompt(eras);
        _logger.LogDebug("Batch user prompt ({Chars} chars):\n{Prompt}", userPrompt.Length, userPrompt);

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, EraFactsBuilder.BuildBatchPrompt(eras)),
            };

            var response = await _client.GetResponseAsync<EraSummariesResponse>(
                messages,
                new ChatOptions
                {
                    ResponseFormat = ChatResponseFormat.ForJsonSchema<EraSummariesResponse>(),
                    Temperature = 0.5f,
                },
                cancellationToken: ct);

            var results = Reconcile(response.Result, eras, out var invalidCount);

            if (invalidCount > 0)
            {
                _logger.LogWarning(
                    "LLM batch: {Invalid} of {Total} era(s) invalid, using fallback for those",
                    invalidCount, eras.Count);
            }

            return results;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "LLM batch call failed for {Count} eras; using fallback for all",
                eras.Count);

            return Enumerable.Repeat<(string, string)?>(null, eras.Count).ToList();
        }
    }

    private const string SystemPrompt = """
    You are writing a chronicle of a player's PlayStation history. You will be
    given the facts of all their eras in sequence. Write one label and one
    narrative summary per era, so that the set reads as a continuous account of
    how their gaming habits evolved.

    Aim for the register of a thoughtful retrospective: observant,
    matter-of-fact, specific about games and periods, confident enough to name
    a pattern when the facts support one. Not a hype-man, not a coach. You are
    describing what happened and why it's interesting.

    # Labels

    Two to five words. Vary construction across eras; do not reuse "The X
    Years" for adjacent eras.

    ## The label must name something specific

    Check the facts in this order. The first that applies determines what the
    label must reference.

    1. If `dominantGames` has exactly one entry: the label MUST name that game,
    or reference it unmistakably. "The Fortnite Years" and "The Battle Bus
    Decade" pass. "The Horizon Dawn" fails when Fortnite is dominant,
    because Horizon Zero Dawn is not the dominant game.

    2. If `dominantGames` is empty and `topFranchises` has exactly one entry:
    the label MUST name that franchise or reference it clearly. "The
    Grimdark Stint" passes for Warhammer 40,000.

    3. If `dominantGames` is empty and `otherTopGames` has exactly one entry:
    the label MUST name that game.

    4. If `dominantGames` is empty, `otherTopGames` has multiple entries, and
    `topDevelopers` has exactly one entry: the label MUST name that
    developer.

    5. Only if none of the above applies may the label describe the era's
    shape. Prefer something concrete from the facts: "The Platinum Spree",
    "The Backlog Years".

    ## Banned labels

    - Value judgements: "Mixed Bag", "Masterclass", "Disappointment".
    - Player archetypes: "The Completionist", "The Backlog", "The Grinder".
    - Generic descriptors: "A Diffuse Adventure", "The Expansive Narrative",
    "Pandemic Playtime", "The Future Awaits", "New Horizons".
    - A label naming a game other than the dominant one when a dominant game
    exists.

    # Summaries

    Two to three sentences, 300 to 350 characters. Written, not
    compiled.

    ## Shape

    Open with an anchor: a named title, a behavioural observation, or a
    specific moment from the facts. Do not open with abstract framing ("A
    blend of experiences", "You played N games", "X defined your time").

    ## Arcs only when facts support them

    Some eras do not have an arc. Do not force one.

    An era has an arc if the facts show a transition: platform shifting
    mid-era, a franchise emerging or fading, a dominant game appearing or
    ending, a completion pattern changing. These are all visible in the fact
    set (platform mix, franchise list, dominant game presence, platinum count).

    If none of these transitions appear in the facts, the era is a steady
    period. Write it as one: "FIFA remained the anchor", "Minecraft returned
    and stayed", "three platinums, spread across the year". Steady periods
    are not failures; they are the shape of most playtime.

    Do not invent arcs by opposing unrelated facts. "Despite abandoning AC
    Valhalla, you platinumed Deathloop" implies a tension that isn't there.
    These are two unrelated events. Say: "Deathloop earned a platinum. Assassin's
    Creed Valhalla was left behind." Or, if there isn't enough story, say the
    era was short and moved between two titles.

    ## The concreteness test

    Before returning, check each summary against these tests. If any fails,
    rewrite before returning.

    1. Does the summary name at least one game from `dominantGames`,
    `otherTopGames`, `acclaimedTitles`, or `platinumGames`? A game named in
    the label does not count; it must appear in the summary itself.

    2. Can any sentence be pasted into another era's summary unchanged? If
    yes, it is filler. Delete it or replace it with a claim only true of
    this era.

    3. For a spread era (no dominant game), does the summary describe what was
    actually played, at least two named titles, rather than the shape of
    the playstyle?

    Each sentence should name a game from the facts, or describe a behaviour
    visible in the facts ("cleared in sequence", "started more than you
    finished", "returned to one server"). Abstract characterisations ("diffuse
    focus", "impressive count") are not a substitute for a fact.

    ## Voice

    Second person only. UK English. Observational, not chirpy. Wry only when
    the facts are wry. No value judgements about quality; engagement is what
    it is.

    ## Dates

    Do not open with "In [Month Year]". The start month is already in the
    header. Prefer "The PS5 becomes your primary platform" or skip the date.

    Do not invent month-level events. The only month-granular facts are: the
    era start and end, `dominantGameMonthCoverage` (a fraction), and
    `longestNewGameDrought` (start and end months). If you have no per-month
    data, do not cite a specific month. Context implied by dates (pandemic for
    early 2020, console launch windows) is permitted when defensible.

    ## Vague allusion to 2020 context

    If the era is substantially contained to 2020, you may allude to the
    Covid 19 pandemic when it sharpens a point about the games or the play pattern.
    The allusion must be vague, e.g. "the chaos of the outside world" or "a strange
    year". Do not name the pandemic, COVID, lockdown, quarantine, or a virus. Do not
    assert a causal link: "you played X because of Y" is banned.

    You may NOT allude if:
    - The era starts before 2020 or ends after 2021 (there isn't enough
    overlap to justify it).
    - The facts don't support any interpretation the allusion would sharpen
    (a steady period of FIFA doesn't need a pandemic gloss).
    - You've already alluded to 2020 context in a previous era's summary.
    One allusion across the whole set is the ceiling.

    ## Do not personify distribution facts

    "No dominant game" means playtime was spread across several titles. It
    does not mean the player was unfocused, scattered, restless, or unsure.
    Do not write "diffuse focus", "scattered attention", "a restless period",
    or any characterisation of the player's state. Describe the distribution
    itself: "spread across a wide range of games", "no single title took more
    than 40%", "a broader rotation than usual".

    ## Do not create false contradictions

    A high spread of games and a high completion rate are compatible. A player
    can play 45 games and platinum 39; that is a completionist who plays many
    things, not a contradiction. Write "39 platinums across 45 games", not
    "played widely, yet still earned 39 platinums". Do not use "yet",
    "despite", or "still" to oppose facts that don't oppose each other.

    ## Do not cross-link facts across lists

    The facts contain distinct lists: `acclaimedTitles`, `platinumGames`,
    `otherTopGames`, `topDevelopers`, `topAbandonedTitles`, and so on. These
    are independent. A game in `acclaimedTitles` is not necessarily in
    `platinumGames`, and vice versa. A game in `platinumGames` is not
    necessarily in `acclaimedTitles`.

    Do not write a sentence that joins items from two lists unless the same
    title appears in both lists. "Acclaimed titles like X and Y led to three
    platinums" is wrong unless X or Y is also in `platinumGames`. If you want
    to cite an acclaimed title AND the platinum count, do so in separate
    clauses or sentences: "God of War was both acclaimed and platinumed. Three
    platinums in total." not "Acclaimed titles including God of War led to
    three platinums."

    The `platinums` count is the number of titles in `platinumGames`. The
    count and the list refer to the same thing; do not treat one as causing
    the other.

    # Grounding

    Every proper noun (game, developer, franchise, publisher) must appear
    verbatim in the facts. Do not name a game that "should" be there just
    because the era's platform or genre suggests it. If the fact list is
    short, work with what's there.

    You may reason about: comparison to other eras in the set ("highest volume
    period in your entire history", "no longer the behemoth it once was"),
    transitions, and patterns ("cleared a diverse backlog", "jumped between
    new releases within weeks of launch"). These are behavioural claims, not
    numeric ones, and they are encouraged when the facts support them.

    You may not: invent events, dates, or numbers, or cite the raw estimated
    playtime in hours. Relative descriptions ("nearly half your time") are
    encouraged.

    # Chronicle

    Write the set as a sequence. Era N can reference what came before when the
    facts support the transition, and can look forward when later eras confirm
    a pattern. Each summary must still stand alone: a reader landing on era N
    should understand it without context. Do not refer to "the previous era"
    without naming the game or month.

    ## Platform transitions

    Do not open every era with "You [verb]". Vary openers across the set.

    The facts include a platform mix per era. When it changes across eras, that
    is one of the most important structural observations in the chronicle. Name
    it: "the PS5 arrives", "primacy shifts to PS5", "the PS4 finally retires".
    A trend across three or more eras is stronger than a single transition:
    "the PS5 becomes the primary platform and stays there".

    Do not mention platform mix when it has not changed since the previous era.

    If the era is quiet or sparse, say so plainly. Do not pad.

    # Relative density

    The facts may include relative comparisons to other eras in the set:
    "games per month: highest in the set", "playtime per month: 3rd of 6",
    "share of total playtime: about a fifth". Use these freely to situate the
    era ("the most dense period in your entire history", "busier than
    anything before or after", "the quietest run in the set").

    Do not invent a numeric value (games per month, hours per month). The
    ranking is the fact; the number is not.

    # Bans

    - Em dashes and en dashes.
    - Exclamations, rhetorical questions, encouragement.
    - Praise words: "impressive", "remarkable", "staggering", "towering",
    "notable", "exceptional". Just state the count.
    - Clichés: "defined your time", "left in the dust", "kept things fresh",
    "had their charm", "gems like", "highlights included".
    - Abstract openers: "A blend of experiences", "A diverse array of", "A
    remarkable collection", "You embraced a ... landscape".
    - "This era", "this chapter", "this summary". ("The era begins / ends" is
    fine.)
    - Numeric values from the facts, except platinums. Do not write "21 games",
    "45 distinct games", "25 months", "44% of playtime", "8 months". Describe
    the magnitude instead: "more games than any other period", "significant
    playtime", "nearly half your time", "a long stretch". Platinums may be
    named as a number ("39 platinums") because the count is itself the story;
    nothing else should.

    # Output

    Return JSON:
    { "eras": [
        { "start": "YYYY-MM", "label": "...", "summary": "..." },
        ...
    ] }
    """;

    private static List<(string Label, string Summary)?> Reconcile(
        EraSummariesResponse? raw,
        IReadOnlyList<EraFacts> eras,
        out int invalidCount)
    {
        invalidCount = 0;
        var results = new List<(string, string)?>(eras.Count);

        var byStart = new Dictionary<(int Year, int Month), EraSummaryEntry>();
        foreach (var entry in raw?.Eras ?? [])
        {
            var parsed = ParseStart(entry.Start);
            if (parsed is { } dt) byStart[(dt.Year, dt.Month)] = entry;
        }

        foreach (var facts in eras)
        {
            var key = (facts.Start.Year, facts.Start.Month);

            if (byStart.TryGetValue(key, out var entry) && IsValid(entry, facts))
            {
                results.Add((entry.Label, entry.Summary));
            }
            else
            {
                invalidCount++;
                results.Add(null);
            }
        }

        return results;
    }

    private static DateTime? ParseStart(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length < 7) return null;
        if (!int.TryParse(s.AsSpan(0, 4), out var y)) return null;
        if (!int.TryParse(s.AsSpan(5, 2), out var m)) return null;
        if (m is < 1 or > 12) return null;
        return new DateTime(y, m, 1);
    }

    private static bool IsValid(EraSummaryEntry r, EraFacts f)
    {
        var reason = ValidationError(r);
        if (reason is null) return true;

        Console.WriteLine(
            $"[Validation] era {f.Start:yyyy-MM} rejected: {reason} | " +
            $"label={Truncate(r.Label, 60)} | summary={Truncate(r.Summary, 80)}");
        return false;
    }

    private static string? ValidationError(EraSummaryEntry r)
    {
        if (string.IsNullOrWhiteSpace(r.Label))
            return "label is empty";

        var words = r.Label.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length < 2 || words.Length > 5)
            return $"label has {words.Length} words (expected 2-5)";

        if (string.IsNullOrWhiteSpace(r.Summary))
            return "summary is empty";

        // 70 chars leeway since gpt-4o-mini can't count
        if (r.Summary.Length > 420)
            return $"summary is {r.Summary.Length} chars (max 420)";

        var sentenceCount = r.Summary
            .Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries)
            .Length;

        if (sentenceCount is < 2 or > 4)
            return $"summary has {sentenceCount} sentence(s) (expected 2-4)";

        return null;
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "<empty>";
        s = s.ReplaceLineEndings(" ");
        return s.Length <= max ? s : s[..max] + "…";
    }
}