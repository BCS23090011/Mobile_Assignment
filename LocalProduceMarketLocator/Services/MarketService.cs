using LocalProduceMarketLocator.Models;

namespace LocalProduceMarketLocator.Services;

public class MarketService : IMarketService
{
    private readonly IDatabaseService _databaseService;
    private readonly IFirebaseService _firebaseService;
    private readonly IAuthService _authService;

    public async Task<bool> HasUserSubmittedMarketsAsync(string userId)
    {
        // 检查 markets 节点 (检查所有市场，看有没有这个用户提交的)
        var allMarkets = await _firebaseService.GetAllMarketsFromCloudAsync();
        var hasMarket = allMarkets.Any(m => m.SubmittedBy == userId);

        // 检查 submissions 节点 (检查删除/修改申请)
        var allSubmissions = await _firebaseService.GetAllSubmissionsFromCloudAsync();
        var hasSubmission = allSubmissions.Any(s => s.SubmittedBy == userId);

        return hasMarket || hasSubmission;
    }

    public MarketService(IDatabaseService databaseService, IFirebaseService firebaseService, IAuthService authService)
    {
        _databaseService = databaseService;
        _firebaseService = firebaseService;
        _authService = authService;
    }

    // 1. 获取市场列表（带同步功能）
    public async Task<List<Market>> GetApprovedMarketsAsync()
    {
        // 先同步：拉取所有 Approved/Rejected/Pending 状态，更新本地记录
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            // 有网：先去云端把最新数据拉下来，更新本地数据库
            await SyncMarketsFromCloudAsync();
        }
        // 再读取：只返回 Approved 的给地图显示
        return await _databaseService.GetMarketsAsync();
    }
    //1/12/25
    public async Task<bool> SubmitDeleteRequestAsync(MarketSubmission submission)
    {
        try
        {
            // 1. 先存本地 (让 My Submissions 列表立马显示出来)
            await _databaseService.SaveSubmissionAsync(submission);

            // 2. 再存云端 (让 Web Admin 看到)
            // 这就是我们刚才加的那个新方法
            return await _firebaseService.SaveSubmissionToCloudAsync(submission);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error submitting request: {ex.Message}");
            return false;
        }
    }
    // Services/MarketService.cs

    // Services/MarketService.cs

    private async Task SyncMarketsFromCloudAsync()
    {
        try
        {
            var currentUser = await _authService.GetCurrentUserAsync();

            // 1. 清空本地旧 Submissions (防止列表重复，保持不变)
            if (currentUser != null)
            {
                var dbSubmissions = await _databaseService.GetAllSubmissionsAsync();
                var myOldSubmissions = dbSubmissions.Where(s => s.SubmittedBy == currentUser.Id).ToList();
                foreach (var oldSub in myOldSubmissions)
                {
                    await _databaseService.DeleteSubmissionAsync(oldSub);
                }
            }

            // 2. 获取云端所有 Markets (Source of Truth)
            var cloudMarkets = await _firebaseService.GetAllMarketsFromCloudAsync();

            // 3. 获取本地所有 Markets (当前的地图数据)
            var localMapMarkets = await _databaseService.GetMarketsAsync();

            // ==========================================================
            // 🔥🔥🔥 4. 关键修复：清理地图上的“孤儿”钉子 (Hard Delete Cleanup)
            // ==========================================================
            var cloudMarketIds = cloudMarkets?.Select(m => m.Id).ToHashSet() ?? new HashSet<string>();

            foreach (var localMarket in localMapMarkets)
            {
                // 如果本地有记录，但它的 ID 不在云端的任何一个市场 ID 列表里，说明它被管理员物理删除了
                if (!cloudMarketIds.Contains(localMarket.Id))
                {
                    await _databaseService.DeleteMarketAsync(localMarket.Id);
                    System.Diagnostics.Debug.WriteLine($"🗑️ Orphan Market Deleted: {localMarket.Name}");
                }
            }

            // ==========================================================
            // 5. 再次处理云端 Markets (Insert/Update 逻辑)
            // ==========================================================
            if (cloudMarkets != null)
            {
                foreach (var m in cloudMarkets)
                {
                    // A. 地图数据更新 (Approved)
                    if (m.Status == "Approved")
                    {
                        await _databaseService.SaveMarketAsync(m);
                    }
                    else
                    {
                        // B. 确保本地状态不是 Approved 的 Market 被删掉 (防止软删除失败)
                        // (这一步会重复执行 Delete，但无妨，确保了 Rejected 的也删了)
                        await _databaseService.DeleteMarketAsync(m.Id);
                    }

                    // C. 列表数据重建 (Submission)
                    if (m.SubmittedBy == currentUser.Id)
                    {
                        var newSub = new MarketSubmission
                        {
                            Id = m.Id,
                            MarketId = m.Id,
                            MarketName = m.Name,
                            SubmittedBy = m.SubmittedBy,
                            SubmittedByName = m.SubmittedByName,
                            Status = m.Status,
                            SubmittedAt = m.SubmittedAt,
                            RequestType = "New"
                        };
                        await _databaseService.SaveSubmissionAsync(newSub);
                    }
                }
            }

            // 6. 处理 Submissions (删除申请) (保持不变)
            var cloudSubmissions = await _firebaseService.GetAllSubmissionsFromCloudAsync();
            if (cloudSubmissions != null)
            {
                foreach (var sub in cloudSubmissions)
                {
                    if (sub.SubmittedBy == currentUser.Id && sub.RequestType == "Delete")
                    {
                        sub.MarketName = $"Delete: {sub.MarketName}";
                        if (string.IsNullOrEmpty(sub.SubmittedByName))
                        {
                            sub.SubmittedByName = currentUser.DisplayName ?? "Unknown";
                        }
                        sub.Id = "DEL_" + sub.MarketId;
                        await _databaseService.SaveSubmissionAsync(sub);
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine("✅ Full Sync Complete: Map Pins are clean.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ Sync failed: {ex.Message}");
        }
    }

    public async Task<Market?> GetMarketByIdAsync(string id)
    {
        return await _databaseService.GetMarketByIdAsync(id);
    }

    public async Task<bool> SubmitMarketAsync(Market market, FileResult? photo)
    {
        try
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null) return false;

            market.SubmittedBy = currentUser.Id;
            market.SubmittedByName = currentUser.DisplayName;
            market.Status = "Pending";

            // 上传图片
            if (photo != null)
            {
                market.PhotoUrl = await _firebaseService.UploadImageAsync(photo);
            }

            // 存本地
            await _databaseService.SaveMarketAsync(market);
            // 存云端
            await _firebaseService.SaveMarketToCloudAsync(market);

            // 创建提交记录
            var submission = new MarketSubmission
            {
                MarketId = market.Id,
                MarketName = market.Name,
                SubmittedBy = currentUser.Id,
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow
            };
            await _databaseService.SaveSubmissionAsync(submission);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> LikeMarketAsync(string marketId, string userId)
    {
        try
        {
            var market = await _databaseService.GetMarketByIdAsync(marketId);
            if (market != null)
            {
                market.Likes++;
                await _databaseService.UpdateMarketAsync(market);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}