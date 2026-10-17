using MyGamingMap.API.Models.DTOs;

namespace MyGamingMap.API.Services;

public class AnalyticsService()
{
    public async Task<Analytics> GenerateAnalytics(List<EnrichedPlayerGame> enrichedPlayerGames)
    {
        var distinctGames = EnrichedGameHelper.MergeByConceptId(enrichedPlayerGames);

        return new Analytics
        {
            Map = MapService.GetMap(enrichedPlayerGames, distinctGames),
            TierList = IGDBAnalyticsService.CalculateReviewRatingAnalytics(enrichedPlayerGames).TierList,
        };
    }
}