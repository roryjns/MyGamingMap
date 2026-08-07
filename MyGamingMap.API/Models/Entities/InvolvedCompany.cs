using System.ComponentModel.DataAnnotations;

namespace MyGamingMap.API.Models.Entities;

// Relationship between game and company
public class InvolvedCompany
{
    [Key] public long Id { get; set; }

    public long GameId { get; set; }

    public Game? Game { get; set; }

    public long CompanyId { get; set; }

    public required Company Company { get; set; }

    public bool Developer { get; set; }

    public bool Publisher { get; set; }
}