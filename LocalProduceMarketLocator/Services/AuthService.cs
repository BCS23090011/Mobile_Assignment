using LocalProduceMarketLocator.Models;
using Newtonsoft.Json;
using System.Net.Http.Json;
using System.Text;

namespace LocalProduceMarketLocator.Services;

public class AuthService : IAuthService
{
    private readonly IDatabaseService _databaseService;
    private readonly HttpClient _httpClient;

    // 1. 填入你截图里的真实 API Key
    private const string FirebaseApiKey = "AIzaSyDLySSqZ8kjuH_5gIl6uEmSCklqQCMdNjE";
    private const string PasswordResetUrl = "https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key=";

    // Firebase 身份验证的标准接口地址
    private const string SignUpUrl = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=";
    private const string SignInUrl = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=";

    private User? _currentUser;

    // 注入 HttpClient 用于发送网络请求
    public AuthService(IDatabaseService databaseService, HttpClient httpClient)
    {
        _databaseService = databaseService;
        _httpClient = httpClient;
    }

    public bool IsAuthenticated => _currentUser != null;
    public string? CurrentUserId => _currentUser?.Id;

    public async Task<bool> RegisterAsync(string email, string password, string displayName)
    {
        try
        {
            // 2. 发送真正的网络请求给 Firebase 注册
            var payload = new { email, password, returnSecureToken = true };
            var response = await _httpClient.PostAsJsonAsync($"{SignUpUrl}{FirebaseApiKey}", payload);
            

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<FirebaseAuthResponse>(content);

                // 创建本地用户对象
                var user = new User
                {
                    Id = result.LocalId, // 使用 Firebase 返回的真实 UID
                    Email = result.Email,
                    DisplayName = displayName,
                    Role = "User",
                    CreatedAt = DateTime.UtcNow
                };

                // 保存到本地数据库做缓存
                await _databaseService.SaveUserAsync(user);
                _currentUser = user;

                // 保存 Token (以后访问数据库要用)
                await SecureStorage.SetAsync("auth_token", result.IdToken);
                await SecureStorage.SetAsync("user_email", user.Email);

                return true;
            }
            else
            {
                // 👇👇👇 关键改动在这里！抓取 Firebase 的详细报错 👇👇👇
                var errorJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"==========================================");
                System.Diagnostics.Debug.WriteLine($"🔥 FIREBASE 注册失败详情: {errorJson}");
                System.Diagnostics.Debug.WriteLine($"==========================================");

                // 如果你想在这里打断点查看 errorJson 也可以
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Register Error: {ex.Message}");
            return false;
        }
    }

    public async Task<User?> LoginAsync(string email, string password)
    {
        try
        {
            // 3. 发送真正的网络请求给 Firebase 登录
            var payload = new { email, password, returnSecureToken = true };
            var response = await _httpClient.PostAsJsonAsync($"{SignInUrl}{FirebaseApiKey}", payload);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<FirebaseAuthResponse>(content);

                // 登录成功，保存 Token
                await SecureStorage.SetAsync("auth_token", result.IdToken);
                await SecureStorage.SetAsync("user_email", result.Email);

                // 尝试从本地获取用户信息，或者创建一个临时的
                var user = await _databaseService.GetUserByEmailAsync(email);
                if (user == null)
                {
                    string defaultName = email.Contains("@") ? email.Split('@')[0] : "User";

                    user = new User
                    {
                        Id = result.LocalId,
                        Email = email,
                        Role = "User",
                        DisplayName = defaultName
                    };
                    await _databaseService.SaveUserAsync(user);
                }

                _currentUser = user;
                return user;
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Login Error: {ex.Message}");
            return null;
        }
    }

    // 👇 2. 实现重置密码的方法
    public async Task<bool> ResetPasswordAsync(string email)
    {
        try
        {
            // 构建 Firebase 需要的 Payload
            // requestType 必须是 "PASSWORD_RESET"
            var payload = new
            {
                requestType = "PASSWORD_RESET",
                email = email
            };

            // 发送请求
            var response = await _httpClient.PostAsJsonAsync($"{PasswordResetUrl}{FirebaseApiKey}", payload);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"🔥 重置密码失败: {errorJson}");
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Reset Password Error: {ex.Message}");
            return false;
        }
    }

    public Task<bool> LogoutAsync()
    {
        try
        {
            _currentUser = null;
            SecureStorage.Remove("auth_token");
            SecureStorage.Remove("user_email");
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        if (_currentUser != null) return _currentUser;

        // 简单的自动登录检查
        var email = await SecureStorage.GetAsync("user_email");
        if (!string.IsNullOrEmpty(email))
        {
            _currentUser = await _databaseService.GetUserByEmailAsync(email);
        }
        return _currentUser;
    }

    // 辅助类：用于解析 Firebase 返回的 JSON
    private class FirebaseAuthResponse
    {
        [JsonProperty("idToken")]
        public string IdToken { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("localId")]
        public string LocalId { get; set; }
    }
}