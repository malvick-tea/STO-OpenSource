using System;
using System.Collections.Generic;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Commander;

/// <summary>
/// Mutable owner of the commander's map state — the chronological list of marks (strokes
/// + tokens) plus the in-progress stroke. The renderer + the input bridge talk to this;
/// the screen owns it and threads it between them.
///
/// Pure data — no I/O, no service deps, no rendering. The <see cref="Changed"/> event
/// fires once per visible mutation (a new stroke begun, an active stroke extended, a
/// stroke finished, a token placed, an undo, a clear), so the future networking layer
/// can subscribe and forward deltas without polling.
///
/// <see cref="Marks"/> is the source of truth — <see cref="Strokes"/> and
/// <see cref="Tokens"/> are filtered views over it. Iterate <c>Marks</c> for ordering;
/// iterate the typed views when rendering needs typed data.
/// </summary>
public sealed class CommanderMapState
{
    private readonly List<CommanderMapMark> _marks = new();
    private readonly List<CommanderMapPoint> _activePoints = new();
    private Color _activeInkColor;
    private int _activeThickness;
    private bool _isDrawing;

    /// <summary>Fires once per mutation that changed the visible state.</summary>
    public event Action? Changed;

    public IReadOnlyList<CommanderMapMark> Marks => _marks;

    public IReadOnlyList<CommanderMapStroke> Strokes => FilterMarks<CommanderMapStroke>();

    public IReadOnlyList<CommanderMapToken> Tokens => FilterMarks<CommanderMapToken>();

    /// <summary>True while a stroke is being drawn — its points live in
    /// <see cref="ActivePoints"/> and are not yet in <see cref="Marks"/>.</summary>
    public bool IsDrawing => _isDrawing;

    public IReadOnlyList<CommanderMapPoint> ActivePoints => _activePoints;

    public Color ActiveInkColor => _activeInkColor;

    public int ActiveThickness => _activeThickness;

    /// <summary>
    /// Starts a new stroke at <paramref name="start"/>. If a stroke is already in progress
    /// it is committed first — defensive against double-press inputs that didn't see an
    /// intervening release. <paramref name="thickness"/> is clamped to a sensible minimum
    /// so an ink-zero stroke can't go invisible.
    /// </summary>
    public void Begin(CommanderMapPoint start, Color inkColor, int thickness)
    {
        if (_isDrawing)
        {
            CommitActiveStroke();
        }

        _activePoints.Clear();
        _activePoints.Add(start);
        _activeInkColor = inkColor;
        _activeThickness = Math.Max(1, thickness);
        _isDrawing = true;
        Changed?.Invoke();
    }

    /// <summary>
    /// Appends a vertex to the active stroke. Same-pixel events are skipped so a held
    /// mouse cursor doesn't bloat the buffer with duplicates; a no-op stays silent on
    /// <see cref="Changed"/>.
    /// </summary>
    public void Extend(CommanderMapPoint point)
    {
        if (!_isDrawing)
        {
            return;
        }

        var last = _activePoints[_activePoints.Count - 1];
        if (last == point)
        {
            return;
        }

        _activePoints.Add(point);
        Changed?.Invoke();
    }

    /// <summary>Commits the active stroke into <see cref="Marks"/> and clears the active
    /// buffer. A no-op when not drawing.</summary>
    public void End()
    {
        if (!_isDrawing)
        {
            return;
        }

        CommitActiveStroke();
        Changed?.Invoke();
    }

    /// <summary>Places a tank token at <paramref name="position"/>. The label is the
    /// caller's responsibility — see <see cref="CommanderMapTokenLabels.Next"/> for the
    /// sequential auto-label the screen uses.</summary>
    public void PlaceToken(CommanderMapPoint position, Color inkColor, string label)
    {
        _marks.Add(new CommanderMapToken(position, inkColor, label));
        Changed?.Invoke();
    }

    /// <summary>Drops the most recent mark — stroke or token, whichever was last. Returns
    /// true on a real undo, false when there's nothing to undo (so callers can swallow a
    /// misfired key without special-casing). An active stroke is NOT undone — release the
    /// button first.</summary>
    public bool Undo()
    {
        if (_marks.Count == 0)
        {
            return false;
        }

        _marks.RemoveAt(_marks.Count - 1);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Wipes every committed mark and the in-progress stroke. The map returns to
    /// blank paper.</summary>
    public void Clear()
    {
        if (_marks.Count == 0 && !_isDrawing)
        {
            return;
        }

        _marks.Clear();
        _activePoints.Clear();
        _isDrawing = false;
        Changed?.Invoke();
    }

    private void CommitActiveStroke()
    {
        var frozen = _activePoints.ToArray();
        _marks.Add(new CommanderMapStroke(frozen, _activeInkColor, _activeThickness));
        _activePoints.Clear();
        _isDrawing = false;
    }

    private IReadOnlyList<TMark> FilterMarks<TMark>()
        where TMark : CommanderMapMark
    {
        var result = new List<TMark>();
        foreach (var mark in _marks)
        {
            if (mark is TMark typed)
            {
                result.Add(typed);
            }
        }

        return result;
    }
}
