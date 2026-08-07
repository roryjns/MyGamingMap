using System.ComponentModel.DataAnnotations;

namespace MyGamingMap.API.Models.Entities;

public class Game
{
    [Key] public long IGDBId { get; set; }

    public string Name { get; set; } = "";
    
    public GameType? GameType { get; set; }

    public string? CoverId { get; set; }

    public string? ESRB_Rating { get; set; }

    public int? PEGI_Rating { get; set; }

    public double? ReviewRating { get; set; }

    public int? ReviewCount { get; set; }

    public string? Storyline { get; set; }

    public string? Summary { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }


    // Many-to-many
    public ICollection<Collection> Collections { get; set; } = [];

    public ICollection<Franchise> Franchises { get; set; } = [];

    public ICollection<GameEngine> GameEngines { get; set; } = [];

    public ICollection<GameMode> GameModes { get; set; } = [];

    public ICollection<Genre> Genres { get; set; } = [];

    public ICollection<InvolvedCompany> InvolvedCompanies { get; set; } = [];

    public ICollection<PlayerPerspective> PlayerPerspectives { get; set; } = [];

    public ICollection<Screenshot> Screenshots { get; set; } = [];

    public ICollection<Theme> Themes { get; set; } = [];


    // One-to-many
    public ICollection<GameMapping> Mappings { get; set; } = [];

    public ICollection<ReleaseDate> ReleaseDates { get; set; } = [];
}