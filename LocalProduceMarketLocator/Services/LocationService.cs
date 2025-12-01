using Microsoft.Maui.ApplicationModel;

namespace LocalProduceMarketLocator.Services;

public class LocationService : ILocationService
{
    private CancellationTokenSource? _cancelTokenSource;
    private bool _isCheckingLocation;

    public bool IsLocationEnabled => true; // original code: public bool IsLocationEnabled => Geolocation.Default.IsSupported;

    public async Task<bool> RequestLocationPermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            
            if (status == PermissionStatus.Granted)
                return true;

            if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
            {
                // Prompt the user to turn on in settings
                return false;
            }

            if (Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>())
            {
                // Prompt the user with additional information as to why the permission is needed
            }

            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            return status == PermissionStatus.Granted;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Location?> GetCurrentLocationAsync()
    {
        try
        {
            _isCheckingLocation = true;

            var request = new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.Medium,
                Timeout = TimeSpan.FromSeconds(30)
            };

            _cancelTokenSource = new CancellationTokenSource();

            var location = await Geolocation.Default.GetLocationAsync(request, _cancelTokenSource.Token);
            return location;
        }
        catch (FeatureNotSupportedException)
        {
            // Handle not supported on device exception
        }
        catch (FeatureNotEnabledException)
        {
            // Handle not enabled on device exception
        }
        catch (PermissionException)
        {
            // Handle permission exception
        }
        catch (Exception)
        {
            // Unable to get location
        }
        finally
        {
            _isCheckingLocation = false;
        }

        return null;
    }
}


