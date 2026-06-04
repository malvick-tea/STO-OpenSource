namespace Garupan.Content;

/// <summary>Content-side sound paths for one tank audio identity. The rendering host
/// consumes the profile without knowing which vehicle or asset pack supplied it.</summary>
public sealed record TankAudioProfile(
    string EngineStartPath,
    string EngineStopPath,
    string EngineRevUpPath,
    string EngineRevDownPath,
    string EngineIdlePath,
    string EngineHighPath,
    string TracksPath,
    string GroundEffectPath,
    string TurretPath,
    string GunPath,
    string ReloadPath,
    bool IsDefault);
