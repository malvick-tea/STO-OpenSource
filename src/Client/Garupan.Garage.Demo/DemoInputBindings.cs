using System.Collections.Generic;
using Opus.Engine.Input;
using Opus.Engine.Pal.Sdl3;
using Opus.Engine.Renderer.Direct3D12.Scene;

namespace Garupan.Garage.Demo;

/// <summary>
/// Wires the demo's keyboard + mouse + window-close events to game commands. Owns the
/// pressed-key set + drag state and exposes per-frame derived inputs (throttle, steering,
/// fire-held) as properties the main loop polls each tick. Close + restart are signalled
/// via <see cref="CloseRequested"/> + <see cref="RestartRequested"/> flags; the host
/// acknowledges the restart via <see cref="ClearRestartRequest"/> once it has rebuilt
/// the sim.
/// </summary>
/// <remarks>
/// Bindings:
/// <list type="bullet">
///   <item><description>ESC or window-close-button → <see cref="CloseRequested"/></description></item>
///   <item><description>W/S → throttle (W = forward), A/D → steering (D = right)</description></item>
///   <item><description>Space (held) → fire intent passed to <see cref="SimTankDriver.SubmitInput"/></description></item>
///   <item><description>R → <see cref="RestartRequested"/></description></item>
///   <item><description>P → <see cref="PauseToggleRequested"/></description></item>
///   <item><description>Up → toggle orbit pause; Left/Right → orbit speed; Down → reset speed</description></item>
///   <item><description>Mouse wheel → camera zoom; left-drag horizontal → orbit phase; vertical → camera pitch</description></item>
/// </list>
/// </remarks>
internal sealed class DemoInputBindings
{
    private readonly HashSet<Key> _pressed = new();
    private readonly int _windowWidth;
    private bool _dragging;

    public DemoInputBindings(SdlWindowService window, GarageSceneController garage, int windowWidth)
    {
        _windowWidth = windowWidth;
        window.CloseRequested += () => CloseRequested = true;
        WireKeyboard(window, garage);
        WireMouse(window, garage);
    }

    public bool CloseRequested { get; private set; }

    public bool RestartRequested { get; private set; }

    public bool PauseToggleRequested { get; private set; }

    public float Throttle => AxisFromKeys(Key.W, Key.S);

    public float Steering => AxisFromKeys(Key.D, Key.A);

    public bool IsFireHeld => _pressed.Contains(Key.Space);

    /// <summary>Cleared by the host once it has rebuilt the sim in response to a pending
    /// restart request, so a subsequent R-press fires a new one.</summary>
    public void ClearRestartRequest() => RestartRequested = false;

    /// <summary>Cleared by the host once it has toggled the <see cref="PauseController"/>
    /// in response to a pending P-press, so the next P-press fires a fresh toggle.</summary>
    public void ClearPauseToggleRequest() => PauseToggleRequested = false;

    private float AxisFromKeys(Key positive, Key negative) =>
        (_pressed.Contains(positive) ? 1f : 0f) - (_pressed.Contains(negative) ? 1f : 0f);

    private void WireKeyboard(SdlWindowService window, GarageSceneController garage)
    {
        window.KeyPressed += key =>
        {
            _pressed.Add(key);
            switch (key)
            {
                case Key.Escape:
                    CloseRequested = true;
                    break;
                case Key.Up:
                    garage.Orbit.TogglePause();
                    break;
                case Key.Left:
                    garage.Orbit.DecreaseSpeed();
                    break;
                case Key.Right:
                    garage.Orbit.IncreaseSpeed();
                    break;
                case Key.Down:
                    garage.Orbit.ResetSpeed();
                    break;
                case Key.R:
                    RestartRequested = true;
                    break;
                case Key.P:
                    PauseToggleRequested = true;
                    break;
            }
        };
        window.KeyReleased += key => _pressed.Remove(key);
    }

    private void WireMouse(SdlWindowService window, GarageSceneController garage)
    {
        window.MouseWheelScrolled += delta => garage.Zoom(delta);
        window.MouseButtonPressed += button =>
        {
            if (button == MouseButton.Left)
            {
                _dragging = true;
            }
        };
        window.MouseButtonReleased += button =>
        {
            if (button == MouseButton.Left)
            {
                _dragging = false;
            }
        };
        window.MouseMoved += (dx, dy) =>
        {
            if (!_dragging)
            {
                return;
            }

            if (dx != 0)
            {
                garage.Orbit.AdvancePhase(dx / (float)_windowWidth);
            }

            if (dy != 0)
            {
                // SDL motion: +Y is screen-down. Drag-up should raise the camera, so negate.
                garage.PitchCamera(-dy);
            }
        };
    }
}
