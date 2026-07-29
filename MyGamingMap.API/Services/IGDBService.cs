using MyGamingMap.API.Models;
using IGDB;
using IGDB.Models;

namespace MyGamingMap.API.Services;

public partial class IGDBService
{
    private readonly IGDBClient igdb;

    private readonly SemaphoreSlim igdbLock = new(1);

    private DateTime lastRequest = DateTime.MinValue;

    private const string gameQueryFields =
        """
        age_ratings.rating_category.rating,
        age_ratings.rating_category.organization.name,
        alternative_names.name,
        collections.name,
        cover.image_id,
        first_release_date,
        franchises.name,
        game_engines.name,
        game_localizations.name,
        game_modes.name,
        game_type,
        game_type.type,
        genres.name,
        id,
        involved_companies.company.name,
        involved_companies.developer,
        involved_companies.publisher,
        name,
        platforms.name,
        player_perspectives.name,
        release_dates.date,
        release_dates.platform.name,
        release_dates.release_region.region,
        storyline,
        summary,
        themes.name,
        total_rating,
        total_rating_count
        """;



    public IGDBService(IConfiguration configuration)
    {
        var clientId = configuration["IGDB:ClientId"];
        var clientSecret = configuration["IGDB:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new Exception("Missing IGDB credentials");
        }

        igdb = new IGDBClient(clientId, clientSecret);
        igdb = IGDBClient.CreateWithDefaults(clientId, clientSecret);
    }

    public async Task<IEnumerable<IGDBGame?>> GetIGDBGames(List<PlayerGame> playerGames)
    {
        var igdbGames = new List<IGDBGame>();

        using var concurrencyLimiter = new SemaphoreSlim(5);

        async Task<T?> ExecuteLimited<T>(Func<Task<T?>> operation)
        {
            await concurrencyLimiter.WaitAsync();

            try
            {
                return await operation();
            }
            finally
            {
                concurrencyLimiter.Release();
            }
        }

        // Stage 1: Name + Platform lookups
        var nameTasks = playerGames.Select(async playerGame => new
        {
            PlayerGame = playerGame,
            IGDBGame = await ExecuteLimited(() =>
                LookupByNameAndPlatform(
                    playerGame.Name,
                    playerGame.Platform))
        });

        var nameResults = await Task.WhenAll(nameTasks);
        var nameMatched = nameResults.Where(r => r.IGDBGame != null).ToList();
        igdbGames.AddRange(nameMatched.Select(r => r.IGDBGame)!);
        Console.WriteLine($"Matched after first pass (name + platform): {nameMatched.Count}/{playerGames.Count}");

        // Stage 2: ConceptId lookups for unmatched games
        var unmatchedGames = nameResults
            .Where(r => r.IGDBGame == null && r.PlayerGame.ConceptId != null)
            .Select(r => r.PlayerGame)
            .ToList();

        var conceptTasks = unmatchedGames.Select(playerGame =>
            LookupByConceptId(
                playerGame.ConceptId!.Value,
                playerGame.Platform
            )
        );

        var conceptResults = await Task.WhenAll(conceptTasks);
        var conceptMatched = conceptResults.Where(g => g != null).ToList();
        igdbGames.AddRange(conceptMatched!);
        Console.WriteLine($"Matched after second pass (conceptId): {nameMatched.Count + conceptMatched.Count}/{playerGames.Count}");
        return igdbGames;
    }

    private async Task<IGDBGame?> LookupByConceptId(int ConceptId, string Platform)
    {
        var externalGames = await ExecuteIGDBRequest(() =>
            igdb.QueryAsync<ExternalGame>(
                IGDBClient.Endpoints.ExternalGames,
                $"""
                fields game;
                where external_game_source = 36 & uid = "{ConceptId}";
                limit 1;
                """
            )
        );

        var externalGame = externalGames.FirstOrDefault();

        if (externalGame != null)
        {
            var platformIds = Platform
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(p => p.Trim() switch
                {
                    "PS4" => [48, 165],   // PlayStation 4 + PlayStation VR
                    "PS5" => [167, 390],  // PlayStation 5 + PlayStation VR2
                    "PS3" => [9],
                    "PSVITA" => [46],
                    _ => Array.Empty<int>()
                })
                .Distinct()
                .ToList() ?? [];

            var platformFilter = string.Join(",", platformIds);

            var games = await ExecuteIGDBRequest(() =>
                igdb.QueryAsync<Game>(
                    IGDBClient.Endpoints.Games,
                    $"""
                fields {gameQueryFields};
                where id = {externalGame.Game.Id}
                & game_type = (0,3,4,6,8,9,10,11)
                & platforms = ({platformFilter});
                limit 1;
                """
                    )
                );

            var game = games.FirstOrDefault();

            if (game != null)
            {
                Console.WriteLine($"Matched ConceptId {ConceptId} to IGDB game {game.Name}");
                return CastToIGDBGame(game);
            }

            Console.WriteLine($"ConceptId {ConceptId} found IGDB game {externalGame.Game.Id}, but platform did not match");
        }

        Console.WriteLine($"Couldn't find IGDB entry for {ConceptId}");
        return null;
    }

    private async Task<IGDBGame?> LookupByNameAndPlatform(string Name, string Platform)
    {
        Name = Name.Replace("\\", "\\\\").Replace("\"", "\\\"");

        var platformIds = Platform
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(p => p.Trim() switch
            {
                "PS4" => [48L, 165L],   // PlayStation 4 + PlayStation VR
                "PS5" => [167L, 390L],  // PlayStation 5 + PlayStation VR2
                "PS3" => [9L],
                "PSVITA" => [46L],
                _ => Array.Empty<long>()
            })
            .Distinct()
            .ToList() ?? [];

        var platformFilter = string.Join(",", platformIds);

        // Filter to only main game, bundle, episode, standalone expansion, remake, remaster, enhanced game and port
        var matches = await ExecuteIGDBRequest(() =>
            igdb.QueryAsync<Game>(
            IGDBClient.Endpoints.Games,
            $"""
            search "{Name}";
            fields {gameQueryFields}, platforms.name;
            where game_type = (0,3,4,6,8,9,10,11);
            limit 100;
            """
            ),
            $"""
            search "{Name}";
            fields {gameQueryFields}, platforms.name;
            where game_type = (0,3,4,6,8,9,10,11);
            limit 100;
            """
        );

        var candidates = matches
        .Select(g =>
        {
            var candidateNames = new List<string> { g.Name };

            if (g.AlternativeNames?.Values != null)
            {
                candidateNames.AddRange(
                    g.AlternativeNames.Values
                        .Where(a => a.Name != null)
                        .Select(a => a.Name!)
                );
            }

            if (g.GameLocalizations?.Values != null)
            {
                candidateNames.AddRange(
                    g.GameLocalizations.Values
                        .Where(l => l.Name != null)
                        .Select(l => l.Name!)
                );
            }

            var similarity = candidateNames.Max(candidate =>
                StringSimilarity.JaroWinkler(
                    Name.ToLowerInvariant(),
                    candidate.ToLowerInvariant()));

            bool hasPlatforms =
                g.Platforms?.Values?.Length > 0;

            bool platformMatches =
                hasPlatforms &&
                g.Platforms!.Values.Any(p =>
                    p.Id.HasValue &&
                    platformIds.Contains(p.Id.Value));

            return new
            {
                Game = g,
                Similarity = similarity,
                PlatformMatches = platformMatches,
                HasPlatforms = hasPlatforms
            };
        })
        .ToList();

        var bestMatch = candidates
            .OrderByDescending(x => x.Similarity)
            .ThenByDescending(x => x.PlatformMatches)
            .ThenBy(x => x.HasPlatforms)   // prefer missing platforms over wrong platforms
            .FirstOrDefault();

        if (bestMatch != null)
        {
            if (bestMatch.Similarity > 0.91)
            {
                //if (bestMatch.Similarity < 1.00) Console.WriteLine($"Fuzzy matched '{Name}' -> '{bestMatch.Game.Name}' ({bestMatch.Similarity:P1})");
                return CastToIGDBGame(bestMatch.Game);
            }
        }

        Console.WriteLine($"Couldn't find IGDB entry for {Name}");
        return null;
    }

    private static IGDBGame CastToIGDBGame(Game game)
    {
        return new IGDBGame
        {
            IGDB_ID = game.Id,
            Name = game.Name ?? "",

            CoverUrl = game.Cover?.Value?.ImageId != null
                ? $"https://images.igdb.com/igdb/image/upload/t_cover_big/{game.Cover.Value.ImageId}.jpg"
                : null,

            Collections = game.Collections?.Values?
                .Select(x => x.Name)
                .Where(x => x != null)
                .ToList() ?? [],

            Franchises = game.Franchises?.Values?
                .Select(x => x.Name)
                .Where(x => x != null)
                .ToList() ?? [],

            GameEngines = game.GameEngines?.Values?
                .Select(x => x.Name)
                .Where(x => x != null)
                .ToList() ?? [],

            GameModes = game.GameModes?.Values?
                .Select(x => x.Name)
                .Where(x => x != null)
                .ToList() ?? [],

            GameType = game.GameType?.Value?.Type,

            Genres = game.Genres?.Values?
                .Select(x => x.Name)
                .Where(x => x != null)
                .ToList() ?? [],

            Themes = game.Themes?.Values?
                .Select(x => x.Name)
                .Where(x => x != null)
                .ToList() ?? [],

            PlayerPerspectives = game.PlayerPerspectives?.Values?
                .Select(x => x.Name)
                .Where(x => x != null)
                .ToList() ?? [],

            Developers = game.InvolvedCompanies?.Values?
                .Where(company => company.Developer == true)
                .Select(company => company.Company?.Value?.Name)
                .Where(name => name != null)
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [],

            Publishers = game.InvolvedCompanies?.Values?
                .Where(company => company.Publisher == true)
                .Select(company => company.Company?.Value?.Name)
                .Where(name => name != null)
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [],

            ReleaseDates = game.ReleaseDates?.Values?
            .Where(release => release.Platform?.Value?.Name?.Contains("PlayStation") == true)
            .Select(release => new Models.ReleaseDate
            {
                Date = release.Date.HasValue
                    ? DateOnly.FromDateTime(release.Date.Value.Date)
                    : null,

                Platform = release.Platform?.Value?.Name switch
                {
                    "PlayStation 3" => "PS3",
                    "PlayStation Vita" => "PSVITA",
                    "PlayStation 4" => "PS4",
                    "PlayStation VR" => "PS4",
                    "PlayStation VR2" => "PS5",
                    "PlayStation 5" => "PS5",
                    _ => release.Platform?.Value?.Name
                },

                Region = release.ReleaseRegion?.Value?.Region
            })
            .ToList() ?? [],

            ESRB_Rating = game.AgeRatings?.Values?
                .FirstOrDefault(rating => rating.RatingCategory?.Value?.Organization?.Value?.Name == "ESRB")
                ?.RatingCategory?.Value?.Rating,

            PEGI_Rating = game.AgeRatings?.Values?
                .FirstOrDefault(rating => rating.RatingCategory?.Value?.Organization?.Value?.Name == "PEGI")
                ?.RatingCategory?.Value?.Rating,

            ReviewRating = game.TotalRating,
            ReviewCount = game.TotalRatingCount,
            Storyline = game.Storyline,
            Summary = game.Summary,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private async Task<T> ExecuteIGDBRequest<T>(Func<Task<T>> request, string? query = null)
    {
        await igdbLock.WaitAsync();

        try
        {
            var elapsed = DateTime.UtcNow - lastRequest;
            var waitTime = TimeSpan.FromMilliseconds(250) - elapsed;
            if (waitTime > TimeSpan.Zero) await Task.Delay(waitTime);
            lastRequest = DateTime.UtcNow;
            return await request();
        }
        catch (RestEase.ApiException ex)
        {
            Console.WriteLine($"IGDB failed: {ex.StatusCode}");
            Console.WriteLine(query);
            throw;
        }
        finally
        {
            igdbLock.Release();
        }
    }
}