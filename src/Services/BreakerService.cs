using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Logging;

namespace SwiftlyS2_Retakes.Services;

public sealed class BreakerService : IBreakerService
{
  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;

  private readonly IConVar<bool> _breakBreakables;
  private readonly IConVar<bool> _openDoors;

  private static readonly HashSet<string> MapsWithPropDynamic = new(StringComparer.OrdinalIgnoreCase)
  {
    "de_vertigo",
    "de_nuke",
    "de_mirage",
  };

  private static readonly HashSet<string> MapsWithFuncButton = new(StringComparer.OrdinalIgnoreCase)
  {
    "de_nuke",
  };

  public BreakerService(ISwiftlyCore core, ILogger logger)
  {
    _core = core;
    _logger = logger;

    _breakBreakables = core.ConVar.CreateOrFind("retakes_break_breakables", "Break map breakables on round start", true);
    _openDoors = core.ConVar.CreateOrFind("retakes_open_doors", "Open prop_door_rotating on round start", false);
  }

  public void HandleRoundStart()
  {
    _logger.LogPluginDebug("Retakes: Breaker HandleRoundStart called. BreakBreakables={Break}, OpenDoors={Doors}",
      _breakBreakables.Value, _openDoors.Value);

    if (!_breakBreakables.Value && !_openDoors.Value)
    {
      return;
    }

    var mapName = (_core.Engine.GlobalVars.MapName.Value ?? string.Empty).Trim();
    _logger.LogPluginDebug("Retakes: Breaker map name = '{MapName}'", mapName);

    var processed = 0;

    if (_breakBreakables.Value)
    {
      processed += BreakByDesignerName("func_breakable");
      processed += BreakByDesignerName("func_breakable_surf");
      processed += BreakByDesignerName("prop.breakable.01");
      processed += BreakByDesignerName("prop.breakable.02");

      if (MapsWithPropDynamic.Contains(mapName))
      {
        processed += BreakByDesignerName("prop_dynamic");
      }

      if (MapsWithFuncButton.Contains(mapName))
      {
        processed += InputByDesignerName("func_button", "Kill");
      }
    }

    if (_openDoors.Value)
    {
      processed += InputByDesignerName("prop_door_rotating", "open");
    }

    _logger.LogPluginDebug("Retakes: Breaker processed {Count} entities total", processed);
  }

  private int BreakByDesignerName(string designerName)
  {
    return InputByDesignerName(designerName, "Break");
  }

  private int InputByDesignerName(string designerName, string input)
  {
    var count = 0;

    foreach (var ent in _core.EntitySystem.GetAllEntitiesByDesignerName<CEntityInstance>(designerName))
    {
      if (ent is null || !ent.IsValid) continue;
      ent.AcceptInput(input, string.Empty);
      count++;
    }

    _logger.LogPluginDebug("Retakes: Breaker found {Count} entities with designerName '{DesignerName}' (input: {Input})",
      count, designerName, input);

    return count;
  }
}
