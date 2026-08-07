namespace MyGamingMap.API.Models.DTOs;

public class IGDBGameResult
{
    public IGDB.Models.Game RawGame { get; set; } = null!;

    public IGDBGame Game { get; set; } = null!;
}