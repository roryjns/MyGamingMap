namespace MyGamingMap.API.Models.DTOs;

// Game data fetched from the IGDB API and stored in the database
public class IGDBGame
{
    public long IGDB_Id { get; set; }

    public string Name { get; set; } = "";

    public string? CoverId { get; set; }

    public List<string> Collections { get; set; } = [];

    public List<string> Franchises { get; set; } = [];

    public List<string> GameEngines { get; set; } = [];

    public List<string> GameModes { get; set; } = [];

    public string? GameType { get; set; }

    public List<string> Genres { get; set; } = [];

    public List<string> ScreenshotIds { get; set; } = [];

    public List<string> Themes { get; set; } = [];

    public List<string> PlayerPerspectives { get; set; } = [];

    public List<string> Developers { get; set; } = [];

    public List<string> Publishers { get; set; } = [];

    public List<ReleaseDate> ReleaseDates { get; set; } = [];

    public string? ESRB_Rating { get; set; }

    public int? PEGI_Rating { get; set; }

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