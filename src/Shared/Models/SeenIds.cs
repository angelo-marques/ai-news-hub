using System.Text.Json.Serialization;

namespace Shared.Models;

public class SeenIds
{
    [JsonPropertyName("ids")]
    public HashSet<string> Ids { get; set; } = [];

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
