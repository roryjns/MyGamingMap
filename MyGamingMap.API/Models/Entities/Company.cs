using System.ComponentModel.DataAnnotations;

namespace MyGamingMap.API.Models.Entities;

public class Company
{
    [Key] public long Id { get; set; }

    public string Name { get; set; } = "";

    public ICollection<InvolvedCompany> InvolvedCompanies { get; set; } = [];

    public string? LogoImageId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}