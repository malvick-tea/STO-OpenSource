using System;
using System.Collections.Generic;
using Garupan.Client.Core.Application;
using Garupan.Localisation;
using Opus.Engine.Input;
using Opus.Localisation;

namespace Garupan.Client.Ui.Screens.Settings;

/// <summary>
/// One rebindable match action: its display label plus the read/rebind pair that gets
/// and sets the bound <see cref="Key"/> on an <see cref="InputBindings"/> snapshot.
/// </summary>
/// <param name="Label">Translation key for the action's name.</param>
/// <param name="Read">Pulls the currently bound key from a bindings snapshot.</param>
/// <param name="Rebind">Produces a snapshot with this action bound to a new key.</param>
public sealed record InputActionEntry(
    TranslationKey Label,
    Func<InputBindings, Key> Read,
    Func<InputBindings, Key, InputBindings> Rebind);

/// <summary>
/// The fixed catalogue of rebindable match actions, in display order. Turret aim (mouse)
/// and the menu / pause key (Esc) are deliberately absent — they are not rebindable.
/// </summary>
public static class InputActionCatalog
{
    public static IReadOnlyList<InputActionEntry> All { get; } = new[]
    {
        new InputActionEntry(
            L10nKeys.Controls.MoveForward,
            bindings => bindings.MoveForward,
            (bindings, key) => bindings with { MoveForward = key }),
        new InputActionEntry(
            L10nKeys.Controls.MoveBackward,
            bindings => bindings.MoveBackward,
            (bindings, key) => bindings with { MoveBackward = key }),
        new InputActionEntry(
            L10nKeys.Controls.SteerLeft,
            bindings => bindings.SteerLeft,
            (bindings, key) => bindings with { SteerLeft = key }),
        new InputActionEntry(
            L10nKeys.Controls.SteerRight,
            bindings => bindings.SteerRight,
            (bindings, key) => bindings with { SteerRight = key }),
        new InputActionEntry(
            L10nKeys.Controls.Fire,
            bindings => bindings.Fire,
            (bindings, key) => bindings with { Fire = key }),
    };
}
