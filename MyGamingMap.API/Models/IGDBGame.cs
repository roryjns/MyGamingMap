namespace MyGamingMap.API.Models;

// Game data fetched from the IGDB API and stored in the database
// Doesn't include different editions or DLCs, only the main game
public class IGDBGame
{
    public long? IGDB_ID { get; set; }

    public string Name { get; set; } = "";

    public string? CoverUrl { get; set; }

    public List<string> Collections { get; set; } = [];

    public List<string> Franchises { get; set; } = [];

    public List<string> GameEngines { get; set; } = [];

    public List<string> GameModes { get; set; } = [];

    public String? GameType { get; set; }

    public List<string> Genres { get; set; } = [];

    public List<string> Themes { get; set; } = [];

    public List<string> PlayerPerspectives { get; set; } = [];

    public List<string> Developers { get; set; } = [];

    public List<string> Publishers { get; set; } = [];

    public List<ReleaseDate> ReleaseDates { get; set; } = [];

    public string? ESRB_Rating { get; set; }

    public string? PEGI_Rating { get; set; }

    public double? ReviewRating { get; set; }

    public int? ReviewCount { get; set; }

    public string? Storyline { get; set; }

    public string? Summary { get; set; }
    
    public DateTimeOffset UpdatedAt { get; set; }
}

public class ReleaseDate
{
    public string? Platform { get; set; }
    
    public DateOnly? Date { get; set; }

    public string? Region { get; set; }
}