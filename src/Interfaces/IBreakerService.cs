namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for breaking map breakables and opening doors.
/// </summary>
public interface IBreakerService
{
  /// <summary>
  /// Handles breaking breakables and opening doors at round start.
  /// </summary>
  void HandleRoundStart();
}
