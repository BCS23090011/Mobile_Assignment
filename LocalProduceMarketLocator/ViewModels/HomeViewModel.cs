using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalProduceMarketLocator.Services;
using LocalProduceMarketLocator.Views;

namespace LocalProduceMarketLocator.ViewModels;


public partial class HomeViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IDatabaseService _databaseService;
    private readonly IFirebaseService _firebaseService;


    // 🔥 1. 新增：判断是否已登录 (用于控制按钮显示)
    [ObservableProperty]
    private bool isUserLoggedIn;

    [ObservableProperty]
    private bool hasUnreadNotifications;

    [ObservableProperty]
    private string userName = "Guest"; // 默认是 Guest

    [ObservableProperty]
    private bool isLoading;

    public HomeViewModel(IAuthService authService, IDatabaseService databaseService, IFirebaseService firebaseService)
    {
        _authService = authService;
        _databaseService = databaseService;
        _firebaseService = firebaseService;
    }

    // --- 核心：加载用户状态 ---
    [RelayCommand]
    public async Task LoadUserAsync()
    {
        IsLoading = true;
        try
        {
            var user = await _authService.GetCurrentUserAsync();

            // 🔥 2. 根据是否有用户，切换界面状态
            if (user != null)
            {
                IsUserLoggedIn = true;
                UserName = !string.IsNullOrEmpty(user.DisplayName) ? user.DisplayName : "User";
            }
            else
            {
                IsUserLoggedIn = false;
                UserName = "Guest";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    // --- 导航：去注册/登录页 ---
    [RelayCommand]
    private async Task NavigateToAuthAsync()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            await Shell.Current.DisplayAlert("Offline", "Please connect to the internet to register or login.", "OK");
            return;
        }

        // 点击 "Register or Login" 直接跳转到 RegisterPage
        await Shell.Current.GoToAsync(nameof(RegisterPage));
    }

    // --- 登出逻辑 ---
    [RelayCommand]
    private async Task LogoutAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
        if (!confirm) return;

        IsLoading = true;
        await _authService.LogoutAsync();

        // 登出后，不要销毁 AppShell，而是重置状态为“游客”
        // 这样体验更丝滑，不会闪退一下
        IsUserLoggedIn = false;
        UserName = "Guest";

        // 可选：登出后清空未读红点
        HasUnreadNotifications = false;

        IsLoading = false;
    }

    // --- 其他原有功能 (保持不变) ---

    public async Task CheckUnreadNotificationsAsync()
    {
        try
        {
            var user = await _authService.GetCurrentUserAsync();
            if (user != null)
            {
                // 1. 先查本地 (和以前一样)
                var localNotifications = await _databaseService.GetNotificationsAsync(user.Id);
                bool hasLocalUnread = localNotifications.Any(n => !n.IsRead);

                // 如果本地已经有未读的，直接亮红点，不需要浪费流量去查云端
                if (hasLocalUnread)
                {
                    HasUnreadNotifications = true;
                    return;
                }

                // 🔥 2. 如果本地都已读，再尝试联网检查云端广播 (新逻辑) 🔥
                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    // 偷偷去云端拉取最新的广播列表
                    var cloudBroadcasts = await _firebaseService.GetBroadcastsFromCloudAsync();

                    // 拿出本地已有的所有通知 ID
                    var localIds = localNotifications.Select(n => n.Id).ToHashSet();

                    // 检查云端是否有任何 ID 是本地没有的？
                    // 如果有，说明有新广播 -> 亮红点！
                    bool hasNewBroadcast = cloudBroadcasts.Any(b => !localIds.Contains(b.Id));

                    if (hasNewBroadcast)
                    {
                        HasUnreadNotifications = true;
                        return;
                    }
                }

                // 3. 如果本地没有未读，云端也没新的，那就灭灯
                HasUnreadNotifications = false;
            }
            else
            {
                HasUnreadNotifications = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking unread: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task NavigateToSubmissionsAsync()
    {
        IsLoading = true;
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await Shell.Current.DisplayAlert("Offline", "Please connect to the internet to check your submission.", "OK");
                return;
            }

            var user = await _authService.GetCurrentUserAsync();
            if (user == null)
            {
                // 如果没登录，提示去登录
                await Shell.Current.DisplayAlert("Login Required", "Please login to view your submissions.", "OK");
                return;
            }

            var mySubmissions = await _databaseService.GetAllSubmissionsAsync();
            var hasSubmissions = mySubmissions.Any(s => s.SubmittedBy == user.Id);

            if (!hasSubmissions)
            {
                await Shell.Current.DisplayAlert("No Submissions", "You haven't submitted any markets yet. \n\nClick '+ Add Location' to start!", "OK");
            }
            else
            {
                await Shell.Current.GoToAsync($"//{nameof(MapPage)}?action=submissions");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", "Unable to check submissions.", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToAddLocationAsync()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            await Shell.Current.DisplayAlert("Offline", "Please connect to the internet to add a merchant.", "OK");
            return;
        }

        var user = await _authService.GetCurrentUserAsync();
        if (user == null)
        {
            await Shell.Current.DisplayAlert("Login Required", "Please login to add a location.", "OK");
            return; // ⛔ 游客禁止入内
        }

        await Shell.Current.GoToAsync($"//{nameof(MapPage)}?action=add");
    }

    [RelayCommand]
    private async Task NavigateToMapAsync()
    {
        await Shell.Current.GoToAsync($"//{nameof(MapPage)}");
    }

    [RelayCommand]
    private async Task NavigateToNotificationsAsync()
    {
        // 如果是游客，点铃铛也应该没反应或者提示登录，这里暂时允许跳转，进去也是空的
        await Shell.Current.GoToAsync(nameof(NoticePage));
    }

    [RelayCommand]
    private void OpenMenu()
    {
        Shell.Current.FlyoutIsPresented = true;
    }
}