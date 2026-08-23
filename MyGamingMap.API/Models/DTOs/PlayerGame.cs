namespace MyGamingMap.API.Models.DTOs;

// The player's PSN data for a game
public class PlayerGame
{
    public string? TitleId { get; set; }

    public string Name { get; set; } = "";

    public string Platform { get; set; } = "";

    public int? ConceptId { get; set; }

    public string? ImageUrl { get; set; }

    public double? PlayHours { get; set; }

    public int? PlayCount { get; set; }

    public DateTime? FirstPlayed { get; set; }

    public DateTime? LastPlayed { get; set; }

    public List<TrophyData> TrophyData { get; set; } = [];
}

public class TrophyData
{
    public string Name { get; set; } = "";

    public string? NpCommunicationId { get; set; }

    public string? TrophyTitleIconUrl { get; set; }

    public string? Platform { get; set; }

    public int Progress { get; set; }

    public TrophyCounts EarnedTrophies { get; set; } = new();

    public TrophyCounts DefinedTrophies { get; set; } = new();

    public DateTime? LastTrophyEarned { get; set; }
}

public class TrophyCounts
{
    public int Bronze { get; set; }
    public int Silver { get; set; }
    public int Gold { get; set; }
    public int Platinum { get; set; }
}