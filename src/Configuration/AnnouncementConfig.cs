using System.Text.Json.Serialization;

namespace SwiftlyS2_Retakes.Configuration;

/// <summary>
/// Configuration for announcements.
/// </summary>
public sealed class AnnouncementConfig
{
  [JsonPropertyName("bombsite-A-img")]
  public string BombsiteAimg { get; set; } = "https://raw.githubusercontent.com/a2Labs-cc/SwiftlyS2-Retakes/refs/heads/master/resources/images/A-Site.png";

  [JsonPropertyName("bombsite-B-img")]
  public string BombsiteBimg { get; set; } = "https://raw.githubusercontent.com/a2Labs-cc/SwiftlyS2-Retakes/refs/heads/master/resources/images/B-Site.png";
}
