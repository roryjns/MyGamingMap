using Microsoft.EntityFrameworkCore;
using MyGamingMap.API.Models.Entities;
using MyGamingMap.API.Models.DTOs;
using MyGamingMap.API.Data;

namespace MyGamingMap.API.Services;

public class DatabaseService(MyGamingMapContext context)
{
    private readonly MyGamingMapContext context = context;

    private static readonly Dictionary<string, string> PlayStationPlatforms = new()
    {
        { "PlayStation 3", "PS3" },
        { "PlayStation Vita", "PSVITA" },
        { "PlayStation 4", "PS4" },
        { "PlayStation VR", "PS4" },
        { "PlayStation 5", "PS5" },
        { "PlayStation VR2", "PS5" }
    };

    public async Task<HashSet<string>> GetFailedLookupKeys()
    {
        var retryThreshold = DateTimeOffset.UtcNow.AddDays(-30);
        var expiredLookups = await context.FailedLookups.Where(f => f.DateAdded < retryThreshold).ToListAsync();
        context.FailedLookups.RemoveRange(expiredLookups);
        await context.SaveChangesAsync();
        return await context.FailedLookups.Select(f => $"{f.Name}|{f.Platform}").ToHashSetAsync();
    }

    public async Task<Dictionary<long, Game>> GetGamesByIGDBIds(IEnumerable<long> igdbIds)
    {
        var ids = igdbIds.Distinct().ToList();

        if (ids.Count == 0) return [];

        return await context.Games
            .AsNoTracking()
            .AsSplitQuery()
            .Where(g => ids.Contains(g.IGDBId))
            .Include(g => g.Collections)
            .Include(g => g.Franchises)
            .Include(g => g.GameEngines)
            .Include(g => g.GameModes)
            .Include(g => g.GameType)
            .Include(g => g.Genres)
            .Include(g => g.PlayerPerspectives)
            .Include(g => g.Screenshots)
            .Include(g => g.Themes)
            .Include(g => g.InvolvedCompanies)
                .ThenInclude(ic => ic.Company)
            .Include(g => g.ReleaseDates)
                .ThenInclude(rd => rd.Region)
            .ToDictionaryAsync(g => g.IGDBId);
    }

    public async Task<long?> GetIGDBId(int? conceptId, IEnumerable<string?> npCommunicationIds)
    {
        var npIds = npCommunicationIds?
            .Where(id => id != null)
            .Distinct()
            .ToList() ?? [];

        // Prefer NP Communication ID because it is more specific
        if (npIds.Count > 0)
        {
            var npMatch = await context.GameMappings
                .Where(m => npIds.Contains(m.NpCommunicationId!))
                .Select(m => (long?)m.IGDBId)
                .FirstOrDefaultAsync();

            if (npMatch != null) return npMatch;
        }

        // Fall back to ConceptId
        if (conceptId != null)
        {
            var conceptMatch = await context.GameMappings
                .Where(m => m.ConceptId == conceptId)
                .Select(m => (long?)m.IGDBId)
                .FirstOrDefaultAsync();

            if (conceptMatch != null) return conceptMatch;
        }

        return null;
    }

    public async Task SaveGame(IGDB.Models.Game game)
    {
        if (game.Id == null) return;

        var existingGame = await context.Games
            .Include(g => g.Collections)
            .Include(g => g.Franchises)
            .Include(g => g.GameEngines)
            .Include(g => g.GameModes)
            .Include(g => g.GameType)
            .Include(g => g.Genres)
            .Include(g => g.InvolvedCompanies)
            .Include(g => g.PlayerPerspectives)
            .Include(g => g.Screenshots)
            .Include(g => g.Themes)
            .Include(g => g.ReleaseDates)
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.IGDBId == game.Id.Value);

        // No update required
        var gameNeedsUpdate = existingGame == null || existingGame.UpdatedAt < game.UpdatedAt;

        var dbGame = existingGame ?? new Game
        {
            IGDBId = game.Id.Value
        };

        if (gameNeedsUpdate)
        {
            dbGame.Name = game.Name ?? "";
            dbGame.CoverId = game.Cover?.Value?.ImageId;

            dbGame.ESRB_Rating = game.AgeRatings?.Values?
                .FirstOrDefault(r =>
                    r.RatingCategory?.Value?.Organization?.Value?.Name == "ESRB")
                ?.RatingCategory?.Value?.Rating;

            dbGame.PEGI_Rating = int.TryParse(
                game.AgeRatings?.Values?
                    .FirstOrDefault(r =>
                        r.RatingCategory?.Value?.Organization?.Value?.Name == "PEGI")
                    ?.RatingCategory?.Value?.Rating,
                out var pegi)
                ? pegi
                : null;

            dbGame.ReviewRating = NormaliseRating(game.TotalRating, game.TotalRatingCount);
            dbGame.ReviewCount = game.TotalRatingCount;
            dbGame.Storyline = game.Storyline;
            dbGame.Summary = game.Summary;
            dbGame.UpdatedAt = game.UpdatedAt ?? DateTimeOffset.MinValue;
            dbGame.ReleaseDates = await GetReleaseDates(game);
            dbGame.Screenshots = await GetOrCreateIGDBEntities(
                game.Screenshots?.Values.Where(s => s.Id != null).Select(s => s.Id!.Value) ?? [],
                id =>
                {
                    var s = game.Screenshots!.Values.First(x => x.Id == id);

                    return new Screenshot
                    {
                        Id = id,
                        ImageId = s.ImageId ?? ""
                    };
                },
                context.Screenshots
            );
        }

        // These always run so shared entities stay updated
        dbGame.Collections = await GetOrCreateIGDBEntities(
            game.Collections?.Values.Where(c => c.Id != null).Select(c => c.Id!.Value) ?? [],
            id =>
            {
                var c = game.Collections!.Values.First(x => x.Id == id);

                return new Collection
                {
                    Id = id,
                    Name = c.Name ?? "",
                    UpdatedAt = c.UpdatedAt ?? DateTimeOffset.MinValue
                };
            },
            context.Collections
        );

        dbGame.Franchises = await GetOrCreateIGDBEntities(
            game.Franchises?.Values.Where(f => f.Id != null).Select(f => f.Id!.Value) ?? [],
            id =>
            {
                var f = game.Franchises!.Values.First(x => x.Id == id);

                return new Franchise
                {
                    Id = id,
                    Name = f.Name ?? "",
                    UpdatedAt = f.UpdatedAt ?? DateTimeOffset.MinValue
                };
            },
            context.Franchises
        );

        dbGame.GameEngines = await GetOrCreateIGDBEntities(
            game.GameEngines?.Values.Where(e => e.Id != null).Select(e => e.Id!.Value) ?? [],
            id =>
            {
                var e = game.GameEngines!.Values.First(x => x.Id == id);

                return new GameEngine
                {
                    Id = id,
                    Name = e.Name ?? "",
                    LogoImageId = e.Logo?.Value?.ImageId,
                    UpdatedAt = e.UpdatedAt ?? DateTimeOffset.MinValue
                };
            },
            context.GameEngines
        );

        dbGame.GameModes = await GetOrCreateIGDBEntities(
            game.GameModes?.Values.Where(m => m.Id != null).Select(m => m.Id!.Value) ?? [],
            id =>
            {
                var m = game.GameModes!.Values.First(x => x.Id == id);

                return new GameMode
                {
                    Id = id,
                    Name = m.Name ?? "",
                    UpdatedAt = m.UpdatedAt ?? DateTimeOffset.MinValue
                };
            },
            context.GameModes
        );

        dbGame.Genres = await GetOrCreateIGDBEntities(
            game.Genres?.Values.Where(g => g.Id != null).Select(g => g.Id!.Value) ?? [],
            id =>
            {
                var g = game.Genres!.Values.First(x => x.Id == id);

                return new Genre
                {
                    Id = id,
                    Name = g.Name ?? "",
                    UpdatedAt = g.UpdatedAt ?? DateTimeOffset.MinValue
                };
            },
            context.Genres
        );

        dbGame.PlayerPerspectives = await GetOrCreateIGDBEntities(
            game.PlayerPerspectives?.Values.Where(p => p.Id != null).Select(p => p.Id!.Value) ?? [],
            id =>
            {
                var p = game.PlayerPerspectives!.Values.First(x => x.Id == id);

                return new PlayerPerspective
                {
                    Id = id,
                    Name = p.Name ?? "",
                    UpdatedAt = p.UpdatedAt ?? DateTimeOffset.MinValue
                };
            },
            context.PlayerPerspectives
        );

        dbGame.Themes = await GetOrCreateIGDBEntities(
            game.Themes?.Values.Where(t => t.Id != null).Select(t => t.Id!.Value) ?? [],
            id =>
            {
                var t = game.Themes!.Values.First(x => x.Id == id);

                return new Theme
                {
                    Id = id,
                    Name = t.Name ?? "",
                    UpdatedAt = t.UpdatedAt ?? DateTimeOffset.MinValue
                };
            },
            context.Themes
        );

        if (game.GameType?.Value != null)
        {
            dbGame.GameType = await GetOrCreateIGDBEntity(
                game.GameType.Value.Id!.Value,
                game.GameType.Value.UpdatedAt,
                () => new GameType
                {
                    Id = game.GameType.Value.Id.Value,
                    Name = game.GameType.Value.Type ?? "",
                    UpdatedAt = game.GameType.Value.UpdatedAt ?? DateTimeOffset.MinValue
                },
                context.GameTypes,
                existing =>
                {
                    existing.Name = game.GameType.Value.Type ?? "";
                }
            );
        }

        dbGame.InvolvedCompanies = await GetInvolvedCompanies(game);

        if (existingGame == null) context.Games.Add(dbGame);

        await context.SaveChangesAsync();
    }

    public async Task SaveGameMapping(PlayerGame playerGame, long igdbId)
    {
        var npCommunicationIds = playerGame.TrophyData?
            .Where(t => t.NpCommunicationId != null)
            .Select(t => t.NpCommunicationId)
            .Distinct()
            .ToList() ?? [];

        if (playerGame.ConceptId == null && npCommunicationIds.Count == 0) return;

        // If no NP Communication IDs exist, save ConceptId only
        if (npCommunicationIds.Count == 0)
        {
            bool exists = await context.GameMappings.AnyAsync(m =>
                m.ConceptId == playerGame.ConceptId &&
                m.NpCommunicationId == null);

            if (!exists)
            {
                context.GameMappings.Add(new GameMapping
                {
                    IGDBId = igdbId,
                    ConceptId = playerGame.ConceptId,
                    NpCommunicationId = null
                });
            }
        }
        else
        {
            // Save a row for each NP Communication ID
            foreach (var npCommunicationId in npCommunicationIds)
            {
                bool exists = await context.GameMappings.AnyAsync(m =>
                    m.ConceptId == playerGame.ConceptId &&
                    m.NpCommunicationId == npCommunicationId);

                if (!exists)
                {
                    context.GameMappings.Add(new GameMapping
                    {
                        IGDBId = igdbId,
                        ConceptId = playerGame.ConceptId,
                        NpCommunicationId = npCommunicationId
                    });
                }
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task SaveFailedLookup(PlayerGame playerGame)
    {
        var existing = await context.FailedLookups
            .FirstOrDefaultAsync(f =>
                f.Name == playerGame.Name &&
                f.Platform == playerGame.Platform);

        if (existing != null)
        {
            existing.AttemptCount++;
            existing.DateAdded = DateTimeOffset.UtcNow;
        }
        else
        {
            context.FailedLookups.Add(new FailedLookup
            {
                Name = playerGame.Name,
                Platform = playerGame.Platform,
                AttemptCount = 1,
                DateAdded = DateTimeOffset.UtcNow,
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task<List<T>> GetOrCreateIGDBEntities<T>(IEnumerable<long> ids, Func<long, T> factory, DbSet<T> dbSet, Action<T, long>? update = null, Func<long, DateTimeOffset?>? updatedAtSelector = null)
    where T : class
    {
        var entities = new List<T>();

        foreach (var id in ids)
        {
            var existing = await dbSet.FindAsync(id);

            if (existing != null)
            {
                update?.Invoke(existing, id);

                entities.Add(existing);
                continue;
            }

            var entity = factory(id);
            dbSet.Add(entity);
            entities.Add(entity);
        }

        return entities;
    }

    private static async Task<T> GetOrCreateIGDBEntity<T>(long id, DateTimeOffset? updatedAt, Func<T> factory, DbSet<T> dbSet, Action<T>? update = null)
    where T : class
    {
        var existing = await dbSet.FindAsync(id);

        if (existing != null)
        {
            if (updatedAt.HasValue)
            {
                var entityUpdatedAt = (DateTimeOffset?)typeof(T).GetProperty("UpdatedAt")?.GetValue(existing);

                if (entityUpdatedAt == null || entityUpdatedAt < updatedAt.Value)
                {
                    update?.Invoke(existing);
                    typeof(T).GetProperty("UpdatedAt")?.SetValue(existing, updatedAt.Value);
                }
            }

            return existing;
        }

        var entity = factory();
        typeof(T).GetProperty("UpdatedAt")?.SetValue(entity, updatedAt ?? DateTimeOffset.MinValue);
        dbSet.Add(entity);

        return entity;
    }

    private async Task<List<Models.Entities.InvolvedCompany>> GetInvolvedCompanies(IGDB.Models.Game game)
    {
        if (game.InvolvedCompanies?.Values == null) return [];

        var involvedCompanies = new List<Models.Entities.InvolvedCompany>();

        foreach (var ic in game.InvolvedCompanies.Values)
        {
            // Only save developers and publishers
            if (!(ic.Developer ?? false) && !(ic.Publisher ?? false)) continue;

            var igdbCompany = ic.Company?.Value;

            if (igdbCompany?.Id == null) continue;

            var company = await GetOrCreateIGDBEntity(
                igdbCompany.Id.Value,
                igdbCompany.UpdatedAt,
                () => new Models.Entities.Company
                {
                    Id = igdbCompany.Id.Value,
                    Name = igdbCompany.Name ?? "",
                    LogoImageId = igdbCompany.Logo?.Value?.ImageId
                },
                context.Companies,
                existing =>
                {
                    existing.Name = igdbCompany.Name ?? "";
                    existing.LogoImageId = igdbCompany.Logo?.Value?.ImageId;
                }
            );

            involvedCompanies.Add(new Models.Entities.InvolvedCompany
            {
                Id = ic.Id!.Value,
                Company = company,
                Developer = ic.Developer ?? false,
                Publisher = ic.Publisher ?? false
            });
        }

        return involvedCompanies;
    }

    private async Task<List<Models.Entities.ReleaseDate>> GetReleaseDates(IGDB.Models.Game game)
    {
        if (game.ReleaseDates?.Values == null) return [];

        var releaseDates = new List<Models.Entities.ReleaseDate>();

        foreach (var r in game.ReleaseDates.Values)
        {
            var platformName = r.Platform?.Value?.Name;

            if (platformName == null || !PlayStationPlatforms.TryGetValue(platformName, out var platform)) continue;

            Models.Entities.Region? region = null;

            var igdbRegion = r.ReleaseRegion?.Value;

            if (igdbRegion?.Id != null)
            {
                region = await GetOrCreateIGDBEntity(
                    igdbRegion.Id.Value,
                    igdbRegion.UpdatedAt,
                    () => new Models.Entities.Region
                    {
                        Id = igdbRegion.Id.Value,
                        Name = igdbRegion.Region ?? "",
                        UpdatedAt = igdbRegion.UpdatedAt?.ToUniversalTime() ?? DateTimeOffset.MinValue
                    },
                    context.Regions,
                    existing =>
                    {
                        existing.Name = igdbRegion.Region ?? "";
                    }
                );
            }

            releaseDates.Add(new Models.Entities.ReleaseDate
            {
                Platform = platform,
                Region = region,
                Date = r.Date.HasValue ? DateOnly.FromDateTime(r.Date.Value.Date) : null
            });
        }

        return releaseDates;
    }

    private static double? NormaliseRating(double? rating, int? reviewCount)
    {
        if (!rating.HasValue || !reviewCount.HasValue) return null;

        const double averageRating = 70; // Adjust based on dataset
        const double minimumReviews = 50;

        var v = reviewCount.Value;
        var R = rating.Value;

        return (v / (v + minimumReviews) * R) + (minimumReviews / (v + minimumReviews) * averageRating);
    }
}