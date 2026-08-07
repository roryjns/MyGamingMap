using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyGamingMap.API.Models.Entities;

// A PSN concept id can have exactly zero or one IGDB id and zero or more np communication ids
// For matching, prefer np communication id if it exists (e.g. games in a collection), otherwise use concept id and take first result
[Index(nameof(ConceptId))]
[Index(nameof(NpCommunicationId))]
public class GameMapping // Used when matching playedGame to IGDBGame
{
    [Key] public long Id { get; set; }

    public long IGDBId { get; set; }

    public Game Game { get; set; } = null!;

    public int? ConceptId { get; set; } = null;

    public string? NpCommunicationId { get; set; } = null;
}

/* Example:
Id | IGDB_Id | ConceptId | NpCommunicationId
---------------------------------------------
1  | 123     | 10001     | NPXX00001
2  | 123     | 10001     | NPXX00002
3  | 456     | null      | NPYY00001
4  | 789     | 35792     | null
*/