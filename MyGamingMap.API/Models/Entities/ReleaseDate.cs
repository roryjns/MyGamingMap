using System.ComponentModel.DataAnnotations;

namespace MyGamingMap.API.Models.Entities;

public class ReleaseDate
{
    [Key] public long Id { get; set; }

    public Game Game { get; set; } = null!;

    public string? Platform { get; set; }

    public DateOnly? Date { get; set; }

    public Region? Region { get; set; }
}