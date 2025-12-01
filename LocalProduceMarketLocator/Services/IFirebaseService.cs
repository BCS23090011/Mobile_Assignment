using LocalProduceMarketLocator.Models;

namespace LocalProduceMarketLocator.Services;

public interface IFirebaseService
{
    Task<bool> SendNotificationAsync(string userId, string title, string body, string type, string? relatedMarketId = null);
    Task<string> UploadImageAsync(FileResult imageFile);
    Task InitializeAsync();
    Task<bool> SaveMarketToCloudAsync(Market market);
    Task<List<Market>> GetAllMarketsFromCloudAsync();
    Task<List<MarketSubmission>> GetAllSubmissionsFromCloudAsync();
    Task<bool> SaveSubmissionToCloudAsync(MarketSubmission submission);
    Task<List<NotificationMessage>> GetNotificationsFromCloudAsync(string userId);
    Task<List<NotificationMessage>> GetBroadcastsFromCloudAsync();
}


