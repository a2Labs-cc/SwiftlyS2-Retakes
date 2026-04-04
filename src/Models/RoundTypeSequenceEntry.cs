namespace SwiftlyS2_Retakes.Models;

/// <summary>
/// A single entry in a round type sequence, defining a type and how many rounds it lasts.
/// </summary>
public sealed class RoundTypeSequenceEntry
{
  /// <summary>
  /// The round type for this entry. Accepted values: Pistol, HalfBuy, FullBuy (case-insensitive).
  /// </summary>
  public string Type { get; set; } = "FullBuy";

  /// <summary>
  /// Number of consecutive rounds this entry applies to.
  /// </summary>
  public int Count { get; set; } = 1;
}
