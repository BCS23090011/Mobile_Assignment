using LocalProduceMarketLocator.Models;

namespace LocalProduceMarketLocator.Services;

public interface IAuthService
{
    Task<bool> RegisterAsync(string email, string password, string displayName);
    Task<User?> LoginAsync(string email, string password);
    Task<bool> LogoutAsync();
    Task<User?> GetCurrentUserAsync();
    bool IsAuthenticated { get; }
    string? CurrentUserId { get; }
}


