namespace LocalProduceMarketLocator.Models;

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "User"; // User or Admin
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


