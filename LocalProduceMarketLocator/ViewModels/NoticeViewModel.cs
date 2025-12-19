using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalProduceMarketLocator.Models;
using LocalProduceMarketLocator.Services;

namespace LocalProduceMarketLocator.ViewModels;

public partial class NoticeViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IDatabaseService _databaseService;
    private readonly IFirebaseService _firebaseService;

    [ObservableProperty]
    private User? currentUser;

    [ObservableProperty]
    private List<NotificationMessage> notifications = new();

    [ObservableProperty]
    private bool isLoading;

    public NoticeViewModel(IAuthService authService, IDatabaseService databaseService, IFirebaseService firebaseService)
    {
        _authService = authService;
        _databaseService = databaseService;
        _firebaseService = firebaseService;
    }

    [RelayCommand]
    public async Task LoadProfileAsync()
    {
        IsLoading = true;
        try
        {
            CurrentUser = await _authService.GetCurrentUserAsync();
            if (CurrentUser != null)
            {
                // 1. 获取私信
                var personalNotes = await _firebaseService.GetNotificationsFromCloudAsync(CurrentUser.Id);

                // 2. 获取广播
                var broadcastNotes = await _firebaseService.GetBroadcastsFromCloudAsync();

                // 3. 合并
                var allCloudNotes = new List<NotificationMessage>();
                allCloudNotes.AddRange(personalNotes);
                allCloudNotes.AddRange(broadcastNotes);

                // 4. 同步到本地
                var localNotifications = await _databaseService.GetNotificationsAsync(CurrentUser.Id);
                var existingIds = localNotifications.Select(n => n.Id).ToHashSet();

                foreach (var cloudNote in allCloudNotes)
                {
                    // 🔥🔥🔥 修改点 1：解决“没有东西显示”的问题 🔥🔥🔥
                    // 这样存入 SQLite 后，GetNotificationsAsync 才能查到它们
                    cloudNote.UserId = CurrentUser.Id;

                    if (!existingIds.Contains(cloudNote.Id))
                    {
                        await _databaseService.SaveNotificationAsync(cloudNote);
                    }
                }

                // 5. 显示
                var finalNotifications = await _databaseService.GetNotificationsAsync(CurrentUser.Id);
                Notifications = finalNotifications.Where(n => !n.IsRead).OrderByDescending(n => n.CreatedAt).ToList();

                // 🔥🔥🔥 修改点 2：解决“点进去就自动变成已读”的问题 🔥🔥🔥
                // 现在的逻辑是：除非你点击消息，或者点击全部清除，否则它们永远保持未读状态。
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Load Notifications Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task MarkNotificationAsReadAsync(NotificationMessage notification)
    {
        // 1. 如果是空的或者已经读过，直接跳过
        if (notification == null || notification.IsRead) return;

        try
        {
            // 2. 更新数据库 (后台记录)
            await _databaseService.MarkNotificationAsReadAsync(notification.Id);

            // 3. 更新内存数据
            notification.IsRead = true;

            // 🔥🔥🔥 核心修复 🔥🔥🔥
            // 这一句就是解决“必须下拉刷新才变已读”的关键。
            // 它强制界面重新渲染列表，红点会立刻消失。
            Notifications = new List<NotificationMessage>(Notifications);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mark Read Error: {ex.Message}");
        }
    }

    // 🔥🔥🔥 修改点 3：新增“清除已读”命令 🔥🔥🔥
    [RelayCommand]
    private void ClearReadNotifications()
    {
        if (Notifications == null) return;

        // 只保留那些 IsRead == false 的（即保留未读的，移除已读的）
        // 这只会改变界面显示，不会删数据库
        Notifications = Notifications.Where(n => !n.IsRead).ToList();
    }
}