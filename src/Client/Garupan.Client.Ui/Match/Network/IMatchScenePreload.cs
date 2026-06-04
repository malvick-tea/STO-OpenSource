namespace Garupan.Client.Ui.Match.Network;

/// <summary>
/// A resumable, frame-sliced preparation of the heavy match-scene assets — the city mesh, the
/// material atlases, and the render-pipeline state. The match screen advances it one step per
/// frame, drawing a progress bar between steps, instead of blocking the render thread for several
/// seconds on the first frame: a multi-second synchronous load reads to the player as a hang.
/// </summary>
/// <remarks>
/// The concrete steps are GPU uploads that live in the D3D12 client; this seam keeps the screen
/// backend-agnostic. <see cref="Advance"/> runs exactly one step, while <see cref="Progress"/> and
/// <see cref="StageLabel"/> drive the loading UI. A renderer whose assets are already resident
/// returns an already-complete preload, so the screen falls straight through to play.
/// </remarks>
public interface IMatchScenePreload
{
    /// <summary>Fraction of the preload completed, in [0, 1]; reaches 1 once every step has run.</summary>
    float Progress { get; }

    /// <summary>Human-readable label of the step that runs next — shown on the loading screen
    /// (e.g. "Loading battlefield"). Empty once the preload is complete.</summary>
    string StageLabel { get; }

    /// <summary>True once every step has run.</summary>
    bool IsComplete { get; }

    /// <summary>Runs the next pending step. Returns true while more steps remain, false once the
    /// preload is complete — including when called on an already-complete preload (a safe no-op).</summary>
    bool Advance();
}
