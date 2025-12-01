namespace LocalProduceMarketLocator.Services;

public interface ILocationService
{
    Task<Location?> GetCurrentLocationAsync();
    Task<bool> RequestLocationPermissionAsync();
    bool IsLocationEnabled { get; }
}


