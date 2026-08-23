using MyGamingMap.API.Models.DTOs;

namespace MyGamingMap.API.Services;

public class AnalyticsService(PSNAnalyticsService psnAnalyticsService, IGDBAnalyticsService iGDBAnalyticsService)
{
    private readonly PSNAnalyticsService psnAnalyticsService = psnAnalyticsService;
    private readonly IGDBAnalyticsService iGDBAnalyticsService = iGDBAnalyticsService;

    public async Task<Analytics> GenerateAnalytics(List<EnrichedPlayerGame> enrichedPlayerGames)
    {
        var playerGames = enrichedPlayerGames.Select(e => e.PlayerGame).ToList();

        return new Analytics
        {
            //PSN = psnAnalyticsService.CalculatePSNAnalytics(enrichedPlayerGames),
            IGDB = iGDBAnalyticsService.CalculateIGDBAnalytics(enrichedPlayerGames)
        };
    }
}