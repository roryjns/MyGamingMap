using System.ComponentModel.DataAnnotations;

namespace MyGamingMap.API.Models.Entities;

public class Screenshot
{
    [Key] public long Id { get; set; }

    public string ImageId { get; set; } = "";

    public long GameId { get; set; }

    public Game Game { get; set; } = null!;
}