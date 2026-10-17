namespace MyGamingMap.API.Services.EraComponents.Rules;

using MyGamingMap.API.Models.DTOs;

public sealed class NewGameDroughtRule : IEraComponentRule
{
    public string DataSource => "callout:new-game-drought";

    public bool IsEligible(EraAnalytics era)
    {
        var drought = era.NewGameDroughts;

        if (drought.DurationDays <= 0)
        {
            Log(era, "ineligible (no drought)");
            return false;
        }

        Log(era, $"callout ({drought.DurationDays} days)");
        return true;
    }

    public EraComponent Build(EraAnalytics era)
    {
        var drought = era.NewGameDroughts!;

        var entries = new List<CalloutEntry>();

        if (drought.LastGameBefore is not null)
        {
            entries.Add(new CalloutEntry
            {
                Label = drought.LastGameBefore.PlayerGame.Name,
                Game = drought.LastGameBefore,
                Value = drought.DurationDays,
                Unit = "day drought",
            });
        }

        if (drought.FirstGameAfter is not null)
        {
            entries.Add(new CalloutEntry
            {
                Label = drought.FirstGameAfter.PlayerGame.Name,
                Game = drought.FirstGameAfter,
                Value = drought.DurationDays,
                Unit = "day drought",
            });
        }

        return new CalloutComponent
        {
            Basis = ComponentBasis.NewGameDrought,
            Description = DescriptionFor(drought),
            Entries = entries,
        };
    }

    private static string DescriptionFor(Drought d) =>
        $"You went {FormatDuration(d.DurationDays)} without starting a new game.";

    private static string FormatDuration(double days) => days switch
    {
        < 14 => $"{days:F0} days",
        < 60 => $"{days / 7.0:F1} weeks",
        < 730 => $"{days / 30.0:F1} months",
        _ => $"{days / 365.0:F1} years",
    };

    private void Log(EraAnalytics era, string decision) =>
        Console.WriteLine(
            $"{GetType().Name} [{era.Start:yyyy-MM} – {era.End:yyyy-MM}]: {decision}");
}