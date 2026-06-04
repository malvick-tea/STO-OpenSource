using System;
using System.Collections.Generic;
using Opus.Foundation;

namespace Garupan.Client.Ui.Match.Network;

/// <summary>One named unit of match-scene preparation: a label for the loading UI plus the work to
/// run. The work is a plain delegate so the stepper stays backend-agnostic — the D3D12 client
/// supplies GPU-upload delegates, tests supply recording no-ops.</summary>
public sealed record MatchPreloadStep(string Label, Action Run);

/// <summary>
/// Pure, backend-agnostic driver for an ordered list of <see cref="MatchPreloadStep"/>s. Runs one
/// step per <see cref="Advance"/> call and reports progress + the active label, so the match screen
/// can fill a loading bar while the heavy GPU assets stream in over several frames rather than in
/// one render-thread-blocking burst.
/// </summary>
public sealed class MatchScenePreload : IMatchScenePreload
{
    private readonly IReadOnlyList<MatchPreloadStep> _steps;
    private int _completed;

    public MatchScenePreload(IReadOnlyList<MatchPreloadStep> steps)
    {
        _steps = Ensure.NotNull(steps);
    }

    /// <summary>An empty step list is complete from the outset (progress 1) — a renderer with no
    /// work to slice hands the screen straight to play.</summary>
    public float Progress => _steps.Count == 0 ? 1f : (float)_completed / _steps.Count;

    public string StageLabel => IsComplete ? string.Empty : _steps[_completed].Label;

    public bool IsComplete => _completed >= _steps.Count;

    public bool Advance()
    {
        if (IsComplete)
        {
            return false;
        }

        _steps[_completed].Run();
        _completed++;
        return !IsComplete;
    }
}
