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
                // 1. 获取私信 (原有)
                var personalNotes = await _firebaseService.GetNotificationsFromCloudAsync(CurrentUser.Id);

                // 2. 🔥 新增：获取广播 (所有人都能看)
                var broadcastNotes = await _firebaseService.GetBroadcastsFromCloudAsync();

                // 3. 合并两个列表
                var allCloudNotes = new List<NotificationMessage>();
                allCloudNotes.AddRange(personalNotes);
                allCloudNotes.AddRange(broadcastNotes);

                // 4. 同步到本地数据库 (逻辑不变，只是源数据变多了)
                var localNotifications = await _databaseService.GetNotificationsAsync(CurrentUser.Id);
                var existingIds = localNotifications.Select(n => n.Id).ToHashSet();

                foreach (var cloudNote in allCloudNotes)
                {
                    // 🚨 重要：对于广播消息，我们需要稍微处理一下 UserId
                    // 因为本地数据库是根据 CurrentUser.Id 过滤显示的
                    // 所以存入本地时，我们要把广播的 UserId 设为当前用户 ID，
                    // 否则 GetNotificationsAsync(user.Id) 查不出来它。
                    if (cloudNote.Type == "Broadcast")
                    {
                        cloudNote.UserId = CurrentUser.Id;
                    }

                    if (!existingIds.Contains(cloudNote.Id))
                    {
                        await _databaseService.SaveNotificationAsync(cloudNote);
                    }
                }

                // 5. 显示
                var finalNotifications = await _databaseService.GetNotificationsAsync(CurrentUser.Id);
                Notifications = finalNotifications.OrderByDescending(n => n.CreatedAt).ToList();

                // 🔥🔥🔥 核心修改：自动把所有未读的标记为已读 🔥🔥🔥
                // 这一步是为了确保当你返回首页时，HomeViewModel 检查发现没有未读消息，从而消除红点
                var unreadNotifications = Notifications.Where(n => !n.IsRead).ToList();

                if (unreadNotifications.Any())
                {
                    foreach (var note in unreadNotifications)
                    {
                        // A. 更新数据库状态
                        await _databaseService.MarkNotificationAsReadAsync(note.Id);

                        // B. 更新内存对象状态 (让当前页面的红点也立刻消失，如果有的话)
                        note.IsRead = true;
                    }
                }
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
        if (notification == null || notification.IsRead)
            return;

        try
        {
            await _databaseService.MarkNotificationAsReadAsync(notification.Id);

            notification.IsRead = true;

            var index = Notifications.IndexOf(notification);
            if (index >= 0)
            {
                Notifications[index] = notification;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mark Read Error: {ex.Message}");
        }
    }
}