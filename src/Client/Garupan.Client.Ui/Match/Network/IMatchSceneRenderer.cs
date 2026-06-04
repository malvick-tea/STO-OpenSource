namespace Garupan.Client.Ui.Match.Network;

/// <summary>
/// Renders one frame of the 3D network match: the ground, the sky, and every tank in the
/// <see cref="NetworkMatchScenePlan"/>, viewed from the plan's camera. Distinct from the
/// engine's minimal <c>IModelRenderer</c> (which draws a single supplied model with no
/// environment) precisely so the match can own a richer scene — a ground plane the tanks
/// sit on — without the garage, which uses <c>IModelRenderer</c>, gaining a floor.
/// </summary>
/// <remarks>
/// The concrete implementation lives in the D3D12 client and composes the engine's
/// runtime scene renderer (floor primitive + tank template + sky) behind this seam, so
/// the screen stays backend-agnostic. The camera + per-tank placements are pure data on
/// the plan (built by <see cref="NetworkMatchSceneProjection"/>); turning them into world
/// matrices is <see cref="MatchSceneInstances"/>.
/// </remarks>
public interface IMatchSceneRenderer
{
    void Render(NetworkMatchScenePlan plan);

    /// <summary>Begins a frame-sliced preparation of the heavy scene assets (city mesh, atlases,
    /// pipeline state). The screen advances the returned preload one step per frame — drawing a
    /// progress bar — before the first <see cref="Render"/>, so the player sees real-time progress
    /// instead of a multi-second freeze. Idempotent: assets brought in by the preload are not
    /// reloaded by <see cref="Render"/>, and a renderer whose assets are already resident returns an
    /// already-complete preload.</summary>
    IMatchScenePreload BeginPreload();

    /// <summary>Ends the active match presentation and releases transient resources such
    /// as looping vehicle audio. Safe to call repeatedly during screen teardown.</summary>
    void EndMatch();
}
