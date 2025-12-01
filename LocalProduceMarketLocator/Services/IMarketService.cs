using LocalProduceMarketLocator.Models;

namespace LocalProduceMarketLocator.Services;

public interface IMarketService
{
    Task<List<Market>> GetApprovedMarketsAsync();
    Task<Market?> GetMarketByIdAsync(string id);
    Task<bool> SubmitMarketAsync(Market market, FileResult? photo);
    Task<bool> LikeMarketAsync(string marketId, string userId);
    Task<bool> SubmitDeleteRequestAsync(MarketSubmission submission); 
    Task<bool> HasUserSubmittedMarketsAsync(string userId);
}


