using LocalProduceMarketLocator.Models;
using SQLite;

namespace LocalProduceMarketLocator.Services;

public class DatabaseService : IDatabaseService
{
    private SQLiteAsyncConnection? _database;
    private const string DatabaseFilename = "LocalProduceMarket.db3";

    public DatabaseService()
    {
    }

    public async Task DeleteSubmissionAsync(MarketSubmission submission)
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAsync(submission);
    }

    public async Task DeleteMarketAsync(string marketId)
    {
        var db = await GetDatabaseAsync();
        // 根据主键 ID 删除
        await db.Table<Market>().DeleteAsync(m => m.Id == marketId);
    }

    public async Task<List<MarketSubmission>> GetPendingSubmissionsAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<MarketSubmission>()
                       .Where(s => s.Status == "Pending")
                       .ToListAsync();
    }

    private async Task<SQLiteAsyncConnection> GetDatabaseAsync()
    {
        if (_database != null)
            return _database;

        var databasePath = Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
        _database = new SQLiteAsyncConnection(databasePath);
        
        await _database.CreateTableAsync<User>();
        await _database.CreateTableAsync<Market>();
        await _database.CreateTableAsync<MarketSubmission>();
        await _database.CreateTableAsync<NotificationMessage>();

        return _database;
    }

    public async Task InitializeAsync()
    {
        await GetDatabaseAsync();
    }

    public async Task SaveUserAsync(User user)
    {
        var db = await GetDatabaseAsync();
        await db.InsertOrReplaceAsync(user);
    }

    public async Task<User?> GetUserByIdAsync(string id)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<User>().Where(u => u.Id == id).FirstOrDefaultAsync();
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<User>().Where(u => u.Email == email).FirstOrDefaultAsync();
    }

    public async Task SaveMarketAsync(Market market)
    {
        var db = await GetDatabaseAsync();
        await db.InsertOrReplaceAsync(market);
    }

    public async Task<List<Market>> GetMarketsAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<Market>().Where(m => m.Status == "Approved").ToListAsync();
    }

    public async Task<Market?> GetMarketByIdAsync(string id)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<Market>().Where(m => m.Id == id).FirstOrDefaultAsync();
    }

    public async Task UpdateMarketAsync(Market market)
    {
        var db = await GetDatabaseAsync();
        await db.UpdateAsync(market);
    }

    public async Task SaveSubmissionAsync(MarketSubmission submission)
    {
        var db = await GetDatabaseAsync();
        await db.InsertOrReplaceAsync(submission);
    }
    public async Task<List<MarketSubmission>> GetAllSubmissionsAsync()
    {
        var db = await GetDatabaseAsync();
        // ❌ 不要 Where，直接返回全部
        return await db.Table<MarketSubmission>().ToListAsync();
    }

    public async Task SaveNotificationAsync(NotificationMessage notification)
    {
        var db = await GetDatabaseAsync();
        await db.InsertOrReplaceAsync(notification);
    }

    public async Task<List<NotificationMessage>> GetNotificationsAsync(string userId)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<NotificationMessage>()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task MarkNotificationAsReadAsync(string notificationId)
    {
        var db = await GetDatabaseAsync();
        var notification = await db.Table<NotificationMessage>()
            .Where(n => n.Id == notificationId)
            .FirstOrDefaultAsync();
        
        if (notification != null)
        {
            notification.IsRead = true;
            await db.UpdateAsync(notification);
        }
    }
}


