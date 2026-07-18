namespace MyGamingMap.API.Models;

// Game data fetched from the IGDB API and stored in the database
public class IGDBGame
{
    public int IGDB_ID { get; set; }

    public string Name { get; set; } = "";

    public DateOnly? ReleaseDate { get; set; }

    public List<string> GameEngine { get; set; } = [];

    public List<string> Genres { get; set; } = [];

    public List<string> Themes { get; set; } = [];

    public List<string> Developers { get; set; } = [];

    public string? Franchise { get; set; }

    public List<string> GameModes { get; set; } = [];
}