using MyGamingMap.API.Models.DTOs;
using IGDB;
using IGDB.Models;
using Polly.RateLimit;
using System.Diagnostics;
using System.Text.Json;

namespace MyGamingMap.API.Services;

public class IGDBService
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
        collections.updated_at,
        cover.image_id,
        first_release_date,
        franchises.name,
        franchises.updated_at,
        game_engines.name,
        game_engines.logo.image_id,
        game_engines.updated_at,
        game_localizations.name,
        game_modes.name,
        game_modes.updated_at,
        game_type.type,
        game_type.updated_at,
        genres.name,
        genres.updated_at,
        id,
        involved_companies.company.name,
        involved_companies.company.logo.image_id,
        involved_companies.company.updated_at,
        involved_companies.developer,
        involved_companies.publisher,
        name,
        parent_game,
        platforms.name,
        player_perspectives.name,
        player_perspectives.updated_at,
        release_dates.date,
        release_dates.platform.name,
        release_dates.release_region.region,
        release_dates.release_region.updated_at,
        screenshots.image_id,
        storyline,
        summary,
        themes.name,
        themes.updated_at,
        total_rating,
        total_rating_count,
        updated_at
        """;

    private readonly DatabaseService databaseService;

    public IGDBService(DatabaseService databaseService, IConfiguration configuration)
    {
        this.databaseService = databaseService;
        var clientId = configuration["IGDB:ClientId"];
        var clientSecret = configuration["IGDB:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret)) throw new Exception("Missing IGDB credentials");

        igdb = new IGDBClient(clientId, clientSecret);
        igdb = IGDBClient.CreateWithDefaults(clientId, clientSecret);
    }

    public async Task<List<EnrichedPlayerGame>> GetEnrichedGames(List<PlayerGame> playerGames)
    {
        var result = await GetIGDBGamesInternal(playerGames, useDatabase: true);

        await File.WriteAllTextAsync(
            "enriched_games.json",
            JsonSerializer.Serialize(
                result.EnrichedGames,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }
            )
        );

        return result.EnrichedGames;
    }

    private async Task<IGDBMatchingResult> GetIGDBGamesInternal(List<PlayerGame> playerGames, bool useDatabase = true)
    {
        var stopwatch = Stopwatch.StartNew();

        // Stage 1: Remove known failures
        var failedLookupKeys = useDatabase
            ? await databaseService.GetFailedLookupKeys()
            : []; var originalPlayerGameCount = playerGames.Count;

        var previouslyFailedGames = playerGames
            .Where(pg =>
                failedLookupKeys.Contains(
                    $"{pg.Name}|{pg.Platform}"
                )
            )
            .ToList();

        var gamesToLookup = playerGames
            .Where(pg =>
                !failedLookupKeys.Contains(
                    $"{pg.Name}|{pg.Platform}"
                )
            )
            .ToList();

        var skippedFailedLookups = previouslyFailedGames.Count;

        Console.WriteLine($"Skipped failed lookups: {skippedFailedLookups}/{originalPlayerGameCount}");

        var enrichedGames = new List<EnrichedPlayerGame>();

        // Previously failed games are still included,
        // but with no IGDB match.
        foreach (var playerGame in previouslyFailedGames)
        {
            enrichedGames.Add(
                new EnrichedPlayerGame
                {
                    PlayerGame = playerGame,
                    IGDBGame = null
                }
            );
        }

        using var concurrencyLimiter = new SemaphoreSlim(6);

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


        int databaseHits = 0;
        int nameLookups = 0;
        int conceptLookups = 0;
        int unmatchedGames = 0;

        // Stage 2: Database mapping lookup
        var databaseMatches = new List<(PlayerGame PlayerGame, long IGDBId)>();
        var unmatchedAfterDatabase = new List<PlayerGame>();

        if (useDatabase)
        {
            foreach (var playerGame in gamesToLookup)
            {
                var igdbId = await databaseService.GetIGDBId(playerGame.ConceptId, playerGame.TrophyData.Select(t => t.NpCommunicationId));
                if (igdbId is long id) databaseMatches.Add((playerGame, igdbId.Value));
                else unmatchedAfterDatabase.Add(playerGame);
            }

            var databaseGameIds = databaseMatches.Select(x => x.IGDBId).Distinct().ToList();
            var databaseGames = await databaseService.GetGamesByIGDBIds(databaseGameIds);

            foreach (var mapping in databaseMatches)
            {
                if (databaseGames.TryGetValue(mapping.IGDBId, out var game))
                {
                    enrichedGames.Add(
                        new EnrichedPlayerGame
                        {
                            PlayerGame = mapping.PlayerGame,
                            IGDBGame = ConvertToIGDBGame(game)
                        }
                    );

                    databaseHits++;
                }
                else
                {
                    unmatchedAfterDatabase.Add(mapping.PlayerGame);
                }
            }
        }
        else
        {
            // With no database, everything proceeds directly to name lookup.
            unmatchedAfterDatabase.AddRange(gamesToLookup);
        }

        Console.WriteLine($"Matched from database: {databaseHits}/{originalPlayerGameCount}");

        // Stage 3: Name + Platform lookups
        nameLookups = unmatchedAfterDatabase.Count;

        var nameLookupTasks = unmatchedAfterDatabase.Select(async playerGame => new
        {
            PlayerGame = playerGame,
            IGDBGame = await ExecuteLimited(() => LookupByName(playerGame.Name, playerGame.Platform))
        });

        var nameLookupResults = await Task.WhenAll(nameLookupTasks);

        var matchedByName = nameLookupResults.Where(r => r.IGDBGame != null).ToList();

        foreach (var result in matchedByName)
        {
            if (useDatabase)
            {
                await databaseService.SaveGame(result.IGDBGame!.RawGame);
                await databaseService.SaveGameMapping(result.PlayerGame, result.IGDBGame.Game.IGDB_Id);
            }

            enrichedGames.Add(
                new EnrichedPlayerGame
                {
                    PlayerGame = result.PlayerGame,
                    IGDBGame = result.IGDBGame!.Game
                }
            );
        }

        var unmatchedAfterName = nameLookupResults
            .Where(r => r.IGDBGame == null)
            .Select(r => r.PlayerGame)
            .ToList();

        var noConceptId = unmatchedAfterName.Count(g => g.ConceptId == null);
        var hasConceptId = unmatchedAfterName.Count(g => g.ConceptId != null);

        Console.WriteLine($"Matched from name lookup: {matchedByName.Count}/{originalPlayerGameCount}");

        Console.WriteLine(
            $"Given up after name lookup (no conceptId): " +
            $"{noConceptId}/{originalPlayerGameCount} " +
            $"({(double)noConceptId / originalPlayerGameCount:P1})");

        // These cannot proceed to ConceptId lookup
        foreach (var playerGame in unmatchedAfterName.Where(g => g.ConceptId == null))
        {
            if (useDatabase)
            {
                await databaseService.SaveFailedLookup(playerGame);
            }

            enrichedGames.Add(
                new EnrichedPlayerGame
                {
                    PlayerGame = playerGame,
                    IGDBGame = null
                }
            );
        }

        // Stage 4: ConceptId lookups
        var conceptLookupGroups = unmatchedAfterName
            .Where(g => g.ConceptId.HasValue)
            .GroupBy(g => new
            {
                g.ConceptId,
                g.Platform
            })
            .ToList();

        conceptLookups = conceptLookupGroups.Count;

        var conceptLookupTasks = conceptLookupGroups.Select(async group => new
        {
            PlayerGames = group.ToList(),

            IGDBGame = await LookupByConceptId(
                group.Key.ConceptId!.Value,
                group.Key.Platform
            )
        });

        var conceptLookupResults = await Task.WhenAll(conceptLookupTasks);

        var matchedByConceptId = conceptLookupResults.Where(r => r.IGDBGame != null).ToList();

        foreach (var result in conceptLookupResults.Where(r => r.IGDBGame != null))
        {
            if (useDatabase) await databaseService.SaveGame(result.IGDBGame!.RawGame);

            foreach (var playerGame in result.PlayerGames)
            {
                if (useDatabase) await databaseService.SaveGameMapping(playerGame, result.IGDBGame!.Game.IGDB_Id);

                enrichedGames.Add(
                    new EnrichedPlayerGame
                    {
                        PlayerGame = playerGame,
                        IGDBGame = result.IGDBGame!.Game
                    }
                );
            }
        }

        foreach (var result in conceptLookupResults.Where(r => r.IGDBGame == null))
        {
            foreach (var playerGame in result.PlayerGames)
            {
                if (useDatabase)
                {
                    await databaseService.SaveFailedLookup(playerGame);
                }

                enrichedGames.Add(
                    new EnrichedPlayerGame
                    {
                        PlayerGame = playerGame,
                        IGDBGame = null
                    }
                );
            }
        }

        // Final statistics
        unmatchedGames = originalPlayerGameCount - databaseHits - matchedByName.Count - matchedByConceptId.Count;

        Console.WriteLine($"Matched from conceptId lookup: {matchedByConceptId.Count}/{hasConceptId} ({(double)matchedByConceptId.Count / hasConceptId:P1})");
        Console.WriteLine($"Given up after conceptId lookup: {unmatchedGames}/{originalPlayerGameCount} ({(double)unmatchedGames / originalPlayerGameCount:P1})");

        foreach (var enrichedGame in enrichedGames)
        {
            // If this PlayerGame has exactly one trophy set, its current name is based on the trophy set name.
            // Prefer the IGDB game's name if it exists.
            if (enrichedGame.PlayerGame.TrophyData.Count == 1 && enrichedGame.IGDBGame != null)
                enrichedGame.PlayerGame.Name = enrichedGame.IGDBGame.Name;

            if (enrichedGame.IGDBGame?.CoverId != null)
                enrichedGame.PlayerGame.ImageUrl = enrichedGame.IGDBGame.CoverId;
        }

        stopwatch.Stop();

        var benchmarkResult =
        new IGDBBenchmarkResult
        {
            ProfileGames = originalPlayerGameCount,
            DatabaseHits = databaseHits,
            NameLookups = nameLookups,
            ConceptIdLookups = conceptLookups,
            UnmatchedGames = unmatchedGames,
            ProcessingTime = stopwatch.Elapsed,
        };

        return new IGDBMatchingResult
        {
            EnrichedGames = enrichedGames,
            BenchmarkResult = benchmarkResult
        };
    }

    public async Task<IGDBBenchmarkResult> GetIGDBGamesBenchmark(List<PlayerGame> playerGames)
    {
        var result = await GetIGDBGamesInternal(playerGames);
        return result.BenchmarkResult;
    }

    private async Task<IGDBGameResult?> LookupByName(string Name, string Platform)
    {
        var searchName = Name.Replace("\\", "\\\\").Replace("\"", "\\\"");

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

        // Filter to only main game, bundle, standalone expansion, episode, remake, remaster, expanded game and port
        var matches = await ExecuteIGDBRequest(() =>
            igdb.QueryAsync<Game>(
            IGDBClient.Endpoints.Games,
            $"""
            search "{searchName}";
            fields {gameQueryFields}, platforms.name;
            where game_type = (0,3,4,6,8,9,10,11);
            limit 100;
            """
            )
        );

        var candidates = matches
        .Where(g =>
        {
            var platforms = g.Platforms?.Values;

            // No platform information: allow
            if (platforms == null || platforms.Length == 0) return true;

            // Platform information exists: require a match
            return platforms.Any(p => p.Id.HasValue && platformIds.Contains(p.Id.Value));
        })
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
            {
                return StringSimilarity.JaroWinkler(NormaliseName(Name), NormaliseName(candidate));
            });

            return new
            {
                Game = g,
                Similarity = similarity
            };
        })
        .ToList();

        // When similarity is tied, prefer the shorter original IGDB name. Prevents an edition being prefered over main game
        var bestMatch = candidates
            .OrderByDescending(x => x.Similarity)
            .ThenBy(x => x.Game.Name?.Length ?? int.MaxValue)
            .FirstOrDefault();

        if (bestMatch != null)
        {
            if (bestMatch.Similarity >= 0.98)
            {
                if (bestMatch.Similarity < 1.00) Console.WriteLine($"Fuzzy matched '{Name}' -> '{bestMatch.Game.Name}' ({bestMatch.Similarity:P1})");

                if (bestMatch.Game.Id != null)
                {
                    return new IGDBGameResult
                    {
                        RawGame = bestMatch.Game,
                        Game = CastToIGDBGame(bestMatch.Game)
                    };
                }
            }
            else
            {
                Console.WriteLine($"Not confident in match '{Name}' -> {bestMatch.Game.Name} ({bestMatch.Similarity:P1})");
                return null;
            }
        }

        Console.WriteLine($"Couldn't find IGDB entry for {Name}");
        return null;
    }

    private async Task<IGDBGameResult?> LookupByConceptId(int ConceptId, string Platform)
    {
        var externalGames = await ExecuteIGDBRequest(() =>
            igdb.QueryAsync<ExternalGame>(
                IGDBClient.Endpoints.ExternalGames,
                $"""
                fields game, platform.name;
                where external_game_source = 36
                & uid = "{ConceptId}";
                limit 100;
                """
            )
        );

        if (externalGames.Length == 0)
        {
            Console.WriteLine($"Couldn't find IGDB external game for ConceptId {ConceptId}");
            return null;
        }

        var gameIds = externalGames
            .Where(e => e.Game?.Id != null)
            .Select(e => e.Game.Id)
            .Distinct()
            .ToList();

        var games = await ExecuteIGDBRequest(() =>
            igdb.QueryAsync<Game>(
                IGDBClient.Endpoints.Games,
                $"""
                fields {gameQueryFields};
                where id = ({string.Join(",", gameIds)})
                & game_type = (0,3,4,6,8,9,10,11);
                limit 100;
                """
            )
        );

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

        var game = games.FirstOrDefault(g =>
        {
            var platforms = g.Platforms?.Values;
            if (platforms == null || platforms.Length == 0) return true;
            return platforms.Any(p => p.Id.HasValue && platformIds.Contains(p.Id.Value));
        });

        if (game != null)
        {
            // If this is an episode with a parent game, use its parent game instead.
            if (game.GameType?.Value?.Type == "Episode" && game.ParentGame?.Id != null)
            {
                var parentGame = (await ExecuteIGDBRequest(() =>
                    igdb.QueryAsync<Game>(
                        IGDBClient.Endpoints.Games,
                        $"""
                        fields {gameQueryFields};
                        where id = {game.ParentGame.Id};
                        limit 1;
                        """
                    ))).FirstOrDefault();

                if (parentGame != null)
                {
                    Console.WriteLine($"Found parent game '{parentGame.Name}' for episode '{game.Name}'");
                    game = parentGame;
                }
            }

            if (game.Id != null)
            {
                return new IGDBGameResult
                {
                    RawGame = game,
                    Game = CastToIGDBGame(game)
                };
            }
        }

        return null;
    }

    private static IGDBGame CastToIGDBGame(Game game)
    {
        if (game.Id == null) throw new InvalidOperationException("Cannot cast IGDB game without an ID");

        return new IGDBGame
        {
            IGDB_Id = game.Id.Value,
            Name = game.Name ?? "",
            CoverId = game.Cover?.Value?.ImageId,

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

            ScreenshotIds = game.Screenshots?.Values?
                .Select(x => x.ImageId)
                .Where(id => id != null)
                .Select(id => id!)
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
            .Select(release => new Models.DTOs.ReleaseDate
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

            PEGI_Rating = int.TryParse(
                    game.AgeRatings?.Values?
                        .FirstOrDefault(rating => rating.RatingCategory?.Value?.Organization?.Value?.Name == "PEGI")
                        ?.RatingCategory?.Value?.Rating,
                    out var pegiRating)
                ? pegiRating
                : null,

            ReviewRating = game.TotalRating,
            ReviewCount = game.TotalRatingCount,
            Storyline = game.Storyline,
            Summary = game.Summary,
            UpdatedAt = game.UpdatedAt ?? DateTimeOffset.MinValue
        };
    }

    private static string NormaliseName(string name)
    {
        var normalised = name.ToLowerInvariant();

        // Japanese punctuation variants
        normalised = normalised
            .Replace("～", "-")
            .Replace("　", " ")
            .Replace("！", "!")
            .Replace("？", "?");

        // Equivalent conjunctions
        // "Ratchet & Clank" == "Ratchet and Clank"
        normalised = normalised
            .Replace("&", " and ")
            .Replace("+", " and ");

        // Remove apostrophes
        // "Collector's" == "Collectors"
        // "Assassin's" == "Assassins"
        normalised = normalised
            .Replace("'", "")
            .Replace("’", "");

        // Normalise separators
        normalised = normalised
            .Replace(":", " ")
            .Replace("-", " ")
            .Replace("–", " ")
            .Replace("—", " ");

        // Collapse whitespace before suffix checking
        normalised = string.Join(
            " ",
            normalised.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries
            )
        );

        // Remove ": the game"
        if (normalised.EndsWith(" the game")) normalised = normalised[..^9].Trim();

        // Remove simple suffixes
        string[] suffixes =
        [
            " hd",
            " dx",
            " ps vita"
        ];

        foreach (var suffix in suffixes)
        {
            if (normalised.EndsWith(suffix))
            {
                normalised = normalised[..^suffix.Length].Trim();
                break;
            }
        }

        // Remove edition variants
        string[] editions =
        [
            " deluxe edition",
            " gold edition",
            " complete edition",
            " definitive edition",
            " digital ultimate edition",
            " ultimate evil edition",
            " game of the year edition",
            " goty edition",
            " collectors edition",
            " legendary edition",
            " premium edition",
            " special edition",
            " enhanced edition",
            " ultimate edition",
            " console edition",
            " arena edition",
            " dawn edition",
            " rustless edition",
            " intruders edition",
            " extended edition",
        ];

        foreach (var edition in editions)
        {
            if (normalised.EndsWith(edition))
            {
                normalised = normalised[..^edition.Length].Trim();
                break;
            }
        }

        // Collapse whitespace again
        normalised = string.Join(
            " ",
            normalised.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries
            )
        );

        return normalised.Trim();
    }

    private async Task<T> ExecuteIGDBRequest<T>(Func<Task<T>> request)
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
        catch (RateLimitRejectedException ex)
        {
            await Task.Delay(ex.RetryAfter);
            return await request();
        }
        catch (RestEase.ApiException ex)
        {
            Console.WriteLine($"IGDB failed: {ex.StatusCode}");
            throw;
        }
        finally
        {
            igdbLock.Release();
        }
    }

    private static IGDBGame ConvertToIGDBGame(Models.Entities.Game game)
    {
        return new IGDBGame
        {
            IGDB_Id = game.IGDBId,
            Name = game.Name,
            CoverId = game.CoverId,
            Collections = [.. game.Collections.Select(c => c.Name)],
            Franchises = [.. game.Franchises.Select(f => f.Name)],
            GameEngines = [.. game.GameEngines.Select(ge => ge.Name)],
            GameModes = [.. game.GameModes.Select(gm => gm.Name)],
            GameType = game.GameType?.Name,
            Genres = [.. game.Genres.Select(g => g.Name)],
            ScreenshotIds = [.. game.Screenshots.Select(s => s.ImageId)],
            Themes = [.. game.Themes.Select(t => t.Name)],
            PlayerPerspectives = [.. game.PlayerPerspectives.Select(pp => pp.Name)],

            Developers = [.. game.InvolvedCompanies
                .Where(ic => ic.Developer)
                .Select(ic => ic.Company?.Name)
                .Where(name => name != null)
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()],

            Publishers = [.. game.InvolvedCompanies
                .Where(ic => ic.Publisher)
                .Select(ic => ic.Company?.Name)
                .Where(name => name != null)
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()],

            ReleaseDates = [.. game.ReleaseDates.Select(rd => new Models.DTOs.ReleaseDate
            {
                Platform = rd.Platform,
                Date = rd.Date,
                Region = rd.Region?.Name
            })],

            ESRB_Rating = game.ESRB_Rating,
            PEGI_Rating = game.PEGI_Rating,
            ReviewRating = game.ReviewRating,
            ReviewCount = game.ReviewCount,
            Storyline = game.Storyline,
            Summary = game.Summary,
            UpdatedAt = game.UpdatedAt
        };
    }
}