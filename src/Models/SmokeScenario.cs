using System.Text.Json.Serialization;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2_Retakes.Utils;

namespace SwiftlyS2_Retakes.Models;

public sealed class SmokeScenario
{
  public int Id { get; set; }
  public string Vector { get; set; } = string.Empty;
  public Bombsite Bombsite { get; set; }
  public string? Name { get; set; }

  [JsonIgnore]
  public Vector Position => ParseUtil.ParseVector(Vector);
}
