// ViewModels/MapViewModel.cs (Final Version)

using Map = Microsoft.Maui.Controls.Maps.Map;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalProduceMarketLocator.Models;
using LocalProduceMarketLocator.Services;
using LocalProduceMarketLocator.Views;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace LocalProduceMarketLocator.ViewModels;

public partial class MapViewModel : ObservableObject, IQueryAttributable
{
    private readonly IMarketService _marketService;
    private readonly ILocationService _locationService;
    private readonly IAuthService _authService;
    private readonly IDatabaseService _databaseService;
    private readonly IFirebaseService _firebaseService; // 🔥 需要注入这个

    private FileResult? _selectedPhotoFile;
    private List<Market> _allMarkets = new();

    // --- Properties ---
    [ObservableProperty] private List<Market> markets = new();
    [ObservableProperty] private Map map;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private Location? currentLocation;
    [ObservableProperty] private Market? selectedMarket;

    // Visibility Flags
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyPopupVisible))]
    private bool isMarketDetailsVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyPopupVisible))]
    private bool isSearchFilterVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyPopupVisible))]
    private bool isAddMerchantVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyPopupVisible))]
    private bool isSubmissionStatusVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyPopupVisible))]
    private bool isReportPopupVisible; // 🔥 新增 Report 弹窗控制

    public bool IsAnyPopupVisible =>
        IsSearchFilterVisible ||
        IsAddMerchantVisible ||
        IsSubmissionStatusVisible ||
        IsMarketDetailsVisible ||
        IsReportPopupVisible;

    // Filter & Search
    [ObservableProperty] private string searchText = string.Empty;
    partial void OnSearchTextChanged(string value) => ApplyFilters();

    [ObservableProperty] private bool isFarmersMarketFilter;
    [ObservableProperty] private bool isOrganicStoreFilter;
    [ObservableProperty] private bool isRoadsideStallFilter;
    [ObservableProperty] private bool isSupermarketFilter;

    [ObservableProperty] private List<MarketSubmission> userSubmissions = new();

    // Add Market Form
    [ObservableProperty] private string newMarketName = string.Empty;
    [ObservableProperty] private string newMarketDetails = string.Empty;
    [ObservableProperty] private string address = string.Empty;
    [ObservableProperty] private string openingHours = string.Empty;
    [ObservableProperty] private string photoPath;
    [ObservableProperty] private string selectedMarketType;
    public List<string> MarketTypes { get; } = new List<string> { "Farmers Market", "Organic Store", "Roadside Stall", "Supermarket Section" };

    // Report Form (新增)
    [ObservableProperty] private string reportEvidence = string.Empty;

    // Constructor
    public MapViewModel(IMarketService marketService, ILocationService locationService, IAuthService authService, IDatabaseService databaseService, IFirebaseService firebaseService)
    {
        _marketService = marketService;
        _locationService = locationService;
        _authService = authService;
        _databaseService = databaseService;
        _firebaseService = firebaseService; // 注入
        Map = new Map();
    }

    public async Task InitializeAsync()
    {
        await LoadMarketsAsync();
        await GetCurrentLocationAsync();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("action"))
        {
            var action = query["action"].ToString();
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(200);
                if (action == "add") NavigateToAddLocation();
                else if (action == "submissions") await OpenSubmissionStatus();
            });
        }
    }

    // --- Commands ---

    [RelayCommand]
    private async Task LoadMarketsAsync()
    {
        IsLoading = true;
        try
        {
            // 1. 获取数据 (这一步内部已经包含了 SyncMarketsFromCloudAsync)
            // MarketService.GetApprovedMarketsAsync 会先同步，再返回本地数据
            // 但是！如果同步还没完成，它可能返回的是旧数据。

            // 为了确保万无一失，我们显式地让 Service 先跑完同步，再拿数据
            // (注意：你需要确保 MarketService.GetApprovedMarketsAsync 里的 await Sync... 是真的在 await)

            _allMarkets = await _marketService.GetApprovedMarketsAsync();

            // 2. 🔥 强制重新应用过滤
            // 这会触发 UpdateMapPins()，把 Rejected 的钉子清理掉
            ApplyFilters();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleFilter(string filterType)
    {
        switch (filterType)
        {
            case "Farmers Market": IsFarmersMarketFilter = !IsFarmersMarketFilter; break;
            case "Organic Store": IsOrganicStoreFilter = !IsOrganicStoreFilter; break;
            case "Roadside Stall": IsRoadsideStallFilter = !IsRoadsideStallFilter; break;
            case "Supermarket Section": IsSupermarketFilter = !IsSupermarketFilter; break;
        }
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = _allMarkets.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var keyword = SearchText.Trim();
            filtered = filtered.Where(m =>
                (m.Name != null && m.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                (m.Description != null && m.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            );
        }

        bool anyTypeSelected = IsFarmersMarketFilter || IsOrganicStoreFilter || IsRoadsideStallFilter || IsSupermarketFilter;
        if (anyTypeSelected)
        {
            filtered = filtered.Where(m =>
                (IsFarmersMarketFilter && m.Type == "Farmers Market") ||
                (IsOrganicStoreFilter && m.Type == "Organic Store") ||
                (IsRoadsideStallFilter && m.Type == "Roadside Stall") ||
                (IsSupermarketFilter && m.Type == "Supermarket Section")
            );
        }

        Markets = filtered.ToList();
        UpdateMapPins();
    }

    [RelayCommand]
    private async Task GetCurrentLocationAsync()
    {
        var hasPermission = await _locationService.RequestLocationPermissionAsync();
        if (!hasPermission) return;
        CurrentLocation = await _locationService.GetCurrentLocationAsync();
        if (CurrentLocation != null)
        {
            Map.MoveToRegion(MapSpan.FromCenterAndRadius(new Microsoft.Maui.Devices.Sensors.Location(CurrentLocation.Latitude, CurrentLocation.Longitude), Distance.FromKilometers(5)));
        }
    }

    [RelayCommand]
    private void MarketSelected(Market market)
    {
        if (market != null)
        {
            SelectedMarket = market;
            IsMarketDetailsVisible = true;
            IsSearchFilterVisible = false;
            IsAddMerchantVisible = false;
            IsSubmissionStatusVisible = false;
            IsReportPopupVisible = false;
        }
    }

    [RelayCommand]
    private void CloseMarketDetails()
    {
        IsMarketDetailsVisible = false;
        SelectedMarket = null;
    }

    [RelayCommand]
    private void ToggleSearchFilter()
    {
        IsSearchFilterVisible = !IsSearchFilterVisible;
        if (IsSearchFilterVisible)
        {
            IsMarketDetailsVisible = false;
            IsAddMerchantVisible = false;
            IsSubmissionStatusVisible = false;
            IsReportPopupVisible = false;
        }
    }

    [RelayCommand]
    private void CloseSearchFilter() => IsSearchFilterVisible = false;

    [RelayCommand]
    private async Task NavigateToAddLocation()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            await Shell.Current.DisplayAlert("Offline", "Please connect to the internet to add a merchant.", "OK");
            return;
        }

        var user = await _authService.GetCurrentUserAsync();
        if (user == null)
        {
            await Shell.Current.DisplayAlert("Login Required", "Please login to add a new location.", "OK");
            return;
        }

        IsSearchFilterVisible = false;
        IsAddMerchantVisible = true;
    }

    [RelayCommand]
    private void CloseAddMerchant() => IsAddMerchantVisible = false;

    [RelayCommand]
    private async Task PickPhoto()
    {
        try
        {
            var photo = await MediaPicker.Default.PickPhotoAsync();
            if (photo != null)
            {
                _selectedPhotoFile = photo;
                PhotoPath = photo.FullPath;
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task SubmitNewMarketAsync()
    {

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            await Shell.Current.DisplayAlert("Connection Lost", "Cannot submit data without internet.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewMarketName))
        {
            await Shell.Current.DisplayAlert("Error", "Please enter a market name", "OK");
            return;
        }

        IsLoading = true;
        try
        {
            var newMarket = new Market
            {
                Name = NewMarketName,
                Description = NewMarketDetails,
                Address = Address,
                Type = SelectedMarketType ?? "General",
                OpeningHours = OpeningHours,
                Latitude = CurrentLocation?.Latitude ?? 0,
                Longitude = CurrentLocation?.Longitude ?? 0,
                SubmittedAt = DateTime.UtcNow,
                //BadgesJson = "[]"
            };

            var success = await _marketService.SubmitMarketAsync(newMarket, _selectedPhotoFile);

            if (success)
            {
                await Shell.Current.DisplayAlert("Success", "Market submitted for review!", "OK");
                CloseAddMerchant();
                NewMarketName = ""; NewMarketDetails = ""; Address = ""; OpeningHours = ""; PhotoPath = null; SelectedMarketType = null; _selectedPhotoFile = null;
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", "Submission failed.", "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task OpenSubmissionStatus()
    {

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            await Shell.Current.DisplayAlert("Offline", "Please connect to the internet to view submissions.", "OK");
            return;
        }

        IsSubmissionStatusVisible = true;
        IsMarketDetailsVisible = false;
        IsSearchFilterVisible = false;
        IsAddMerchantVisible = false;
        IsReportPopupVisible = false;

        IsLoading = true;
        await LoadMarketsAsync();

        var user = await _authService.GetCurrentUserAsync();
        if (user != null)
        {
            var allSubmissions = await _databaseService.GetAllSubmissionsAsync();
            UserSubmissions = allSubmissions.Where(s => s.SubmittedBy == user.Id).ToList();
        }
        else
        {
            await Shell.Current.DisplayAlert("Login Required", "Please login to view submissions.", "OK");
            IsSubmissionStatusVisible = false;
        }
    }

    [RelayCommand]
    private async Task CloseSubmissionStatus()
    {
        IsSubmissionStatusVisible = false;

        // 🔥🔥🔥 【新增】关掉列表时，顺便刷新一下地图 🔥🔥🔥
        // 这样如果列表里有状态变成了 Rejected，地图上的钉子就会立马消失
        await LoadMarketsAsync();
    }

    [RelayCommand]
    private async Task NavigateToSubmissions() => await OpenSubmissionStatus();

    [RelayCommand]
    private async Task NavigateToHome() => await Shell.Current.GoToAsync($"//{nameof(HomePage)}");

    [RelayCommand]
    private void OpenMenu() => Shell.Current.FlyoutIsPresented = true;

    [RelayCommand]
    private async Task NavigateToMarket()
    {
        if (SelectedMarket == null) return;

        try
        {
            // 1. 获取经纬度
            var location = new Location(SelectedMarket.Latitude, SelectedMarket.Longitude);

            // 2. 设置导航选项 (比如地名)
            var options = new MapLaunchOptions
            {
                Name = SelectedMarket.Name,
                NavigationMode = NavigationMode.Driving // 默认驾驶模式，也可以不设
            };

            // 3. 打开外部地图应用 (Google Maps / Apple Maps)
            await Microsoft.Maui.ApplicationModel.Map.Default.OpenAsync(location, options);
        }
        catch (Exception ex)
        {
            // 防御性代码：比如模拟器上没有地图应用
            await Shell.Current.DisplayAlert("Error", "Unable to open map application.", "OK");
        }
    }

    // 🔥🔥🔥 Report / Delete 逻辑 🔥🔥🔥

    [RelayCommand]
    private async Task ReportMarket()// 点击详情页红色按钮
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            await Shell.Current.DisplayAlert("Offline", "Please connect to the internet to report changes.", "OK");
            return;
        }

        var user = await _authService.GetCurrentUserAsync();
        if (user == null)
        {
            await Shell.Current.DisplayAlert("Login Required", "Please login to report changes.", "OK");
            return;
        }

        if (SelectedMarket == null) return;
        IsMarketDetailsVisible = false;
        IsReportPopupVisible = true; // 打开 Report 弹窗
    }

    [RelayCommand]
    private void CloseReportPopup()
    {
        IsReportPopupVisible = false;
        ReportEvidence = string.Empty;
        _selectedPhotoFile = null;
        PhotoPath = null;
    }

    [RelayCommand]
    private async Task SubmitReportAsync()
    {

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            await Shell.Current.DisplayAlert("Connection Lost", "Cannot submit report without internet.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(ReportEvidence))
        {
            await Shell.Current.DisplayAlert("Error", "Please provide details.", "OK");
            return;
        }

        IsLoading = true;
        try
        {
            string evidencePhotoUrl = "";
            if (_selectedPhotoFile != null)
            {
                evidencePhotoUrl = await _firebaseService.UploadImageAsync(_selectedPhotoFile);
            }

            var currentUser = await _authService.GetCurrentUserAsync();
            var submission = new MarketSubmission
            {
                MarketId = SelectedMarket.Id,
                MarketName = SelectedMarket.Name,
                SubmittedBy = currentUser?.Id ?? "Anonymous",

                // 🔥🔥🔥 【新增】把名字赋进去！🔥🔥🔥
                SubmittedByName = currentUser?.DisplayName ?? "Unknown User",

                Status = "Pending",
                RequestType = "Delete",
                ChangeDetails = ReportEvidence + (string.IsNullOrEmpty(evidencePhotoUrl) ? "" : $" [Photo: {evidencePhotoUrl}]"),
                SubmittedAt = DateTime.UtcNow
            };

            var success = await _marketService.SubmitDeleteRequestAsync(submission);

            if (success)
            {
                await Shell.Current.DisplayAlert("Success", "Report submitted for review.", "OK");
                CloseReportPopup();
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", "Failed to submit report.", "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally { IsLoading = false; }
    }

    private void UpdateMapPins()
    {
        Map.Pins.Clear();
        foreach (var market in Markets)
        {
            var pin = new Pin
            {
                Label = market.Name,
                Address = market.Address,
                Location = new Microsoft.Maui.Devices.Sensors.Location(market.Latitude, market.Longitude),
                Type = PinType.Place
            };
            pin.MarkerClicked += (s, e) => { MarketSelected(market); e.HideInfoWindow = true; };
            Map.Pins.Add(pin);
        }
    }
}