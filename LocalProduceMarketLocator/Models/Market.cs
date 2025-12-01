using SQLite;
using Newtonsoft.Json;

namespace LocalProduceMarketLocator.Models;

public class Market
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Type { get; set; } = string.Empty;
    public string OpeningHours { get; set; } = string.Empty;
    public string SubmittedBy { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";
    public string PhotoUrl { get; set; } = string.Empty;

    public int Likes { get; set; } = 0;
    //public int Likes { get; set; } = 0;
    //public bool IsSeasonalHighlight { get; set; } = false;
    //public string SeasonalNote { get; set; } = string.Empty;

}