namespace SwiftlyS2_Retakes.Configuration;

/// <summary>
/// Configuration for weapons.
/// </summary>
public sealed class WeaponsConfig
{
  public bool BuyMenuEnabled { get; set; } = true;

  public List<string> Pistols { get; set; } = new()
  {
    "weapon_glock",
    "weapon_usp_silencer",
    "weapon_hkp2000",
    "weapon_p250",
    "weapon_fiveseven",
    "weapon_tec9",
    "weapon_cz75a",
    "weapon_deagle",
    "weapon_revolver",
    "weapon_elite",
  };

  public RoundWeaponsConfig HalfBuy { get; set; } = new()
  {
    T = new() { "weapon_galilar", "weapon_mac10", "weapon_mp7" },
    Ct = new() { "weapon_famas", "weapon_mac10", "weapon_mp7" },
  };

  public RoundWeaponsConfig FullBuy { get; set; } = new()
  {
    T = new() { "weapon_ak47", "weapon_sg556" },
    Ct = new() { "weapon_m4a1", "weapon_m4a1_silencer", "weapon_aug" },
  };
}

/// <summary>
/// Configuration for weapons per round type.
/// </summary>
public sealed class RoundWeaponsConfig
{
  public List<string> All { get; set; } = new();
  public List<string> T { get; set; } = new();
  public List<string> Ct { get; set; } = new();
}
