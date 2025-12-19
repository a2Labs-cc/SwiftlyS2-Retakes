using System.Globalization;
using SwiftlyS2.Shared.Natives;

namespace SwiftlyS2_Retakes.Utils;

public static class ParseUtil
{
  public static Vector ParseVector(string value)
  {
    var parts = SplitTriple(value);
    return new Vector(
      ParseFloat(parts[0]),
      ParseFloat(parts[1]),
      ParseFloat(parts[2])
    );
  }

  public static QAngle ParseQAngle(string value)
  {
    var parts = SplitTriple(value);
    return new QAngle(
      ParseFloat(parts[0]),
      ParseFloat(parts[1]),
      ParseFloat(parts[2])
    );
  }

  private static string[] SplitTriple(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      throw new FormatException("Value is empty");
    }

    var cleaned = value.Replace(",", string.Empty);
    var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length != 3)
    {
      throw new FormatException($"Expected 3 components, got {parts.Length}");
    }

    return parts;
  }

  private static float ParseFloat(string value)
  {
    return float.Parse(value, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
  }
}
