using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyGamingMap.API.Models.Entities;

[Index(nameof(Name), nameof(Platform), IsUnique = true)]
public class FailedLookup
{
    [Key] public long Id { get; set; }

    public string Name { get; set; } = "";

    public string Platform { get; set; } = "";

    public int AttemptCount { get; set; }

    public DateTimeOffset DateAdded { get; set; }
}