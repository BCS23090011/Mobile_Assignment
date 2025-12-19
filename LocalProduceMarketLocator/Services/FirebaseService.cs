using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using LocalProduceMarketLocator.Models;
using System.Net.Http.Headers;
using System.Text; // 👈 记得引用这个，用于 Encoding.UTF8
using System.Text.Json;
using System.Net.Http.Json;

namespace LocalProduceMarketLocator.Services;

public class FirebaseService : IFirebaseService
{
    private readonly IDatabaseService _databaseService;
    private readonly HttpClient _httpClient;

    // Storage 配置
    private const string StorageBucket = "mobile-44ff2.firebasestorage.app"; // 你的 Bucket
    private const string FirebaseStorageBaseUrl = $"https://firebasestorage.googleapis.com/v0/b/{StorageBucket}/o";


    private const string FirebaseDatabaseUrl = "https://mobile-44ff2-default-rtdb.firebaseio.com/";

    public async Task<List<MarketSubmission>> GetAllSubmissionsFromCloudAsync()
    {
        try
        {
            // 注意：这里读取的是 "submissions.json" 节点
            var response = await _httpClient.GetFromJsonAsync<Dictionary<string, MarketSubmission>>($"{FirebaseDatabaseUrl}submissions.json");

            if (response == null) return new List<MarketSubmission>();

            return response.Values.ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Sync Submissions Error: {ex.Message}");
            return new List<MarketSubmission>();
        }
    }

    public async Task<bool> SaveSubmissionToCloudAsync(MarketSubmission submission)
    {
        try
        {
            var token = await SecureStorage.GetAsync("auth_token");

            // 我们把所有申请单存到 "/submissions/{marketId}" 这个路径下
            // 这样方便 Web Admin 根据 MarketId 找到对应的申请
            var url = $"{FirebaseDatabaseUrl}submissions/{submission.MarketId}.json?auth={token}";

            var json = JsonSerializer.Serialize(submission);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 使用 PUT，直接覆盖（防止同一个人对同一个店重复提交多次申请）
            var response = await _httpClient.PutAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"🔥 Submission Upload Success: {submission.RequestType}");
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Submission Upload Failed: {error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return false;
        }
    }

    public FirebaseService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
        _httpClient = new HttpClient();
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }
    public async Task<bool> SendNotificationAsync(string userId, string title, string body, string type, string? relatedMarketId = null)
    {
        // 这一部分暂时保持本地模拟，因为 FCM 推送需要复杂的 Server Key 配置
        try
        {
            var notification = new NotificationMessage
            {
                UserId = userId,
                Title = title,
                Body = body,
                Type = type,
                RelatedMarketId = relatedMarketId,
                CreatedAt = DateTime.UtcNow
            };

            await _databaseService.SaveNotificationAsync(notification);
            await Toast.Make(body, ToastDuration.Short).Show();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> UploadImageAsync(FileResult imageFile)
    {
        try
        {
            if (imageFile == null) return string.Empty;

            // 1. 生成唯一文件名
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";

            // 2. 构造上传 URL (注意：Upload 不需要 token，但需要 storage rules 允许 write)
            var uploadUrl = $"{FirebaseStorageBaseUrl}?name={fileName}";

            // 3. 读取文件流
            using var stream = await imageFile.OpenReadAsync();
            var content = new StreamContent(stream);

            // 设置 Content-Type (Firebase 需要知道这是图片)
            content.Headers.ContentType = new MediaTypeHeaderValue(imageFile.ContentType ?? "image/jpeg");

            // 4. 发送 POST 请求到 Firebase
            var response = await _httpClient.PostAsync(uploadUrl, content);

            if (response.IsSuccessStatusCode)
            {
                // 5. 解析 Firebase 返回的 JSON
                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                // 获取 Firebase 生成的 Access Token
                var downloadToken = root.GetProperty("downloadTokens").GetString();

                // 6. 拼接成可公开访问的 URL
                // 格式: base_url/filename?alt=media&token=xxx
                var downloadUrl = $"{FirebaseStorageBaseUrl}/{fileName}?alt=media&token={downloadToken}";

                Console.WriteLine($"Upload Success: {downloadUrl}");
                return downloadUrl;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Firebase Upload Failed: {response.StatusCode} - {error}");
                // 如果是 403 Forbidden，通常是 Storage Rules 没设置好
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception during upload: {ex.Message}");
            return string.Empty;
        }
    }

    public async Task<bool> SaveMarketToCloudAsync(Market market)
    {
        try
        {
            // 1. 获取登录 Token (因为你的数据库规则通常要求 auth != null)
            var token = await SecureStorage.GetAsync("auth_token");

            // 2. 构造 URL
            // 使用 PUT 方法到 /markets/{id}.json 可以指定 ID，保持本地 SQLite ID 和云端一致
            // 格式: BaseUrl + "markets/" + ID + ".json?auth=" + token
            var url = $"{FirebaseDatabaseUrl}markets/{market.Id}.json?auth={token}";

            // 3. 序列化对象
            var json = JsonSerializer.Serialize(market);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 4. 发送 PUT 请求
            var response = await _httpClient.PutAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"🔥 Firebase DB Upload Success: {market.Name}");
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Firebase DB Upload Failed: {response.StatusCode} - {error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception during DB upload: {ex.Message}");
            return false;
        }
    }

    // Services/FirebaseService.cs
    public async Task<List<Market>> GetAllMarketsFromCloudAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<Dictionary<string, Market>>($"{FirebaseDatabaseUrl}markets.json");
            if (response == null) return new List<Market>();

            // 我们需要知道哪些被 Rejected 了，所以必须全部拉下来
            var allMarkets = response.Values.ToList();

            System.Diagnostics.Debug.WriteLine($"🔥 Sync: Downloaded {allMarkets.Count} markets from cloud.");
            return allMarkets;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Sync Error: {ex.Message}");
            return new List<Market>();
        }
    }

    public async Task<List<NotificationMessage>> GetNotificationsFromCloudAsync(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId)) return new List<NotificationMessage>();

            // 注意：这里我们只读取属于当前登录用户的通知
            var url = $"{FirebaseDatabaseUrl}notifications/{userId}.json";

            // 所以返回的数据结构是 Dictionary<string, NotificationMessage>
            var response = await _httpClient.GetFromJsonAsync<Dictionary<string, NotificationMessage>>(url);

            if (response == null) return new List<NotificationMessage>();

            // 将字典的值转为列表返回
            return response.Values.ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Sync Notifications Error: {ex.Message}");
            // 出错时返回空列表，不让 App 崩溃
            return new List<NotificationMessage>();
        }
    }

    // 实现新方法
    public async Task<List<NotificationMessage>> GetBroadcastsFromCloudAsync()
    {
        try
        {
            // 读取 notifications/broadcast 节点
            var url = $"{FirebaseDatabaseUrl}notifications/broadcast.json";
            var response = await _httpClient.GetFromJsonAsync<Dictionary<string, NotificationMessage>>(url);

            if (response == null) return new List<NotificationMessage>();
            return response.Values.ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Sync Broadcasts Error: {ex.Message}");
            return new List<NotificationMessage>();
        }
    }
}