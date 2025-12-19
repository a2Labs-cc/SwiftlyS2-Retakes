using System.Text.Json.Serialization;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Utils;

namespace SwiftlyS2_Retakes.Models;

public sealed class Spawn
{
  public string Vector { get; set; } = string.Empty;
  public string QAngle { get; set; } = string.Empty;
  public Team Team { get; set; }
  public Bombsite Bombsite { get; set; }
  public bool CanBePlanter { get; set; }
  public int Id { get; set; }
  public string? Name { get; set; }

  [JsonIgnore]
  public Vector Position => ParseUtil.ParseVector(Vector);

  [JsonIgnore]
  public QAngle Angle => ParseUtil.ParseQAngle(QAngle);
}
