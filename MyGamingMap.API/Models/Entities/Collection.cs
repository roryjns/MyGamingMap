using System.ComponentModel.DataAnnotations;

namespace MyGamingMap.API.Models.Entities;

public class Collection
{
    [Key] public long Id { get; set; }

    public string Name { get; set; } = "";

    public ICollection<Game> Games { get; set; } = [];

    public DateTimeOffset UpdatedAt { get; set; }
}