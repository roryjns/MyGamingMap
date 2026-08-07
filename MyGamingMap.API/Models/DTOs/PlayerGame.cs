namespace MyGamingMap.API.Models.DTOs;

// The player's PSN data for a game
public class PlayerGame
{
    public string? TitleId { get; set; }

    public string Name { get; set; } = "";

    public string Platform { get; set; } = "";

    public int? ConceptId { get; set; }

    public string? Service { get; set; }

    public string? ImageUrl { get; set; }

    public double? PlayHours { get; set; }

    public int? PlayCount { get; set; }

    public DateOnly? FirstPlayed { get; set; }

    public DateOnly? LastPlayed { get; set; }

    public List<TrophyData> TrophyData { get; set; } = [];
}

public class TrophyData
{
    public string Name { get; set; } = "";

    public string? NpCommunicationId { get; set; }

    public string? TrophyTitleIconUrl { get; set; }

    public string? Platform { get; set; }

    public int Progress { get; set; }

    public EarnedTrophies EarnedTrophies { get; set; } = new();
}

public class EarnedTrophies
{
    public int Bronze { get; set; }
    public int Silver { get; set; }
    public int Gold { get; set; }
    public int Platinum { get; set; }
}