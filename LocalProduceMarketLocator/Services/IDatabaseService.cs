using LocalProduceMarketLocator.Models;

namespace LocalProduceMarketLocator.Services;

public interface IDatabaseService
{
    Task InitializeAsync();
    Task SaveUserAsync(User user);
    Task<User?> GetUserByIdAsync(string id);
    Task<User?> GetUserByEmailAsync(string email);
    Task SaveMarketAsync(Market market);
    Task<List<Market>> GetMarketsAsync();
    Task<Market?> GetMarketByIdAsync(string id);
    Task UpdateMarketAsync(Market market);
    Task SaveSubmissionAsync(MarketSubmission submission);
    Task<List<MarketSubmission>> GetPendingSubmissionsAsync();
    Task SaveNotificationAsync(NotificationMessage notification);
    Task<List<NotificationMessage>> GetNotificationsAsync(string userId);
    Task MarkNotificationAsReadAsync(string notificationId);
    Task<List<MarketSubmission>> GetAllSubmissionsAsync();

    Task DeleteSubmissionAsync(MarketSubmission submission);
    Task DeleteMarketAsync(string marketId);
}


