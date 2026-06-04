namespace Garupan.Content;

/// <summary>Authoring measurements for shell models shared by the match and garage
/// renderers. Ballistics retain real diameter and mass in the ammunition catalogue; this
/// display-only calibration keeps a fast GLB shell readable from the chase camera.</summary>
public static class ShellVisualGeometry
{
    /// <summary>The normalized GLB is 1.0 m long and about 0.317 m across. The visual scale
    /// is deliberately exaggerated; hit geometry and ballistics do not use it.</summary>
    public const float PzGr39DisplayScale = 0.85f;
}
