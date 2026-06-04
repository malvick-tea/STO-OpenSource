using System;
using System.Collections.Generic;
using Garupan.Client.Core.Application;
using Opus.Engine.Input;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Settings;

/// <summary>
/// Data model for <see cref="ControlsScreen"/>: the live <see cref="InputBindings"/>, the
/// row cursor over <see cref="InputActionCatalog.All"/>, and the rebind "listening" state.
/// Pure — no service or VFS dependency — so it is unit-testable directly; the screen owns
/// the bridge to <c>SettingsService</c> via the <see cref="Changed"/> event.
/// </summary>
/// <remarks>
/// Conflict policy: capturing a key already held by another action swaps the two, so the
/// binding set stays a bijection — no action is ever left sharing or missing a key.
/// </remarks>
public sealed class ControlsModel
{
    private const int NotListening = -1;

    private readonly IReadOnlyList<InputActionEntry> _actions = InputActionCatalog.All;
    private InputBindings _bindings;
    private int _selectedRow;
    private int _listeningRow = NotListening;

    public ControlsModel(InputBindings initial)
    {
        _bindings = Ensure.NotNull(initial);
    }

    /// <summary>Raised once per coherent rebind, carrying the new snapshot.</summary>
    public event Action<InputBindings>? Changed;

    public InputBindings Bindings => _bindings;

    public IReadOnlyList<InputActionEntry> Actions => _actions;

    public int SelectedRow => _selectedRow;

    /// <summary>True while waiting for a keypress to bind to <see cref="ListeningRow"/>.</summary>
    public bool IsListening => _listeningRow != NotListening;

    /// <summary>The row awaiting a keypress, or -1 when not listening.</summary>
    public int ListeningRow => _listeningRow;

    /// <summary>Moves the row cursor by ±1, clamped. Ignored while listening.</summary>
    public void MoveSelection(int direction)
    {
        if (IsListening)
        {
            return;
        }

        _selectedRow = Math.Clamp(_selectedRow + Math.Sign(direction), 0, _actions.Count - 1);
    }

    /// <summary>Parks the cursor on <paramref name="row"/> (e.g. follows the mouse).
    /// Out-of-range rows, and any call made while listening, are ignored.</summary>
    public void SelectRow(int row)
    {
        if (!IsListening && row >= 0 && row < _actions.Count)
        {
            _selectedRow = row;
        }
    }

    /// <summary>Enters listening mode for the selected row. A call made while already
    /// listening is ignored.</summary>
    public void BeginRebind()
    {
        if (!IsListening)
        {
            _listeningRow = _selectedRow;
        }
    }

    /// <summary>Leaves listening mode without changing any binding.</summary>
    public void CancelRebind() => _listeningRow = NotListening;

    /// <summary>
    /// Binds <paramref name="key"/> to the listening row and leaves listening mode. A
    /// no-op when not listening, when <paramref name="key"/> is unbindable, or when the
    /// row already holds that key. If another action holds <paramref name="key"/> the two
    /// swap. Raises <see cref="Changed"/> only on a real change.
    /// </summary>
    public void CaptureKey(Key key)
    {
        if (!IsListening)
        {
            return;
        }

        var row = _listeningRow;
        _listeningRow = NotListening;

        // Key.None is the null key; Esc is reserved for cancelling the rebind itself.
        if (key == Key.None || key == Key.Escape)
        {
            return;
        }

        var previousKey = _actions[row].Read(_bindings);
        if (key == previousKey)
        {
            return;
        }

        var next = _actions[row].Rebind(_bindings, key);
        for (var other = 0; other < _actions.Count; other++)
        {
            if (other != row && _actions[other].Read(_bindings) == key)
            {
                next = _actions[other].Rebind(next, previousKey);
            }
        }

        _bindings = next;
        Changed?.Invoke(_bindings);
    }
}
