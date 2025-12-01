namespace LocalProduceMarketLocator.Models;
using SQLite;

public class MarketSubmission
{
    [PrimaryKey] // 🔥🔥🔥 必须加这个！🔥🔥🔥
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string MarketName { get; set; } = string.Empty;
    public string MarketId { get; set; } = string.Empty;
    public string SubmittedBy { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public string? RejectionReason { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }

    // 🔥🔥🔥 【新增】区分是 "New" (新店) 还是 "Delete" (请求删除)
    public string RequestType { get; set; } = "New";

    // 🔥🔥🔥 【新增】用来存用户写的证据/理由
    public string ChangeDetails { get; set; } = string.Empty;
}


