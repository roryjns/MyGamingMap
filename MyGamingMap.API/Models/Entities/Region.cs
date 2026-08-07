using System.ComponentModel.DataAnnotations;

namespace MyGamingMap.API.Models.Entities;

public class Region
{
    [Key] public long Id { get; set; }

    public required string Name { get; set; }

    public ICollection<ReleaseDate> ReleaseDates { get; set; } = [];

    public DateTimeOffset UpdatedAt { get; set; }
}