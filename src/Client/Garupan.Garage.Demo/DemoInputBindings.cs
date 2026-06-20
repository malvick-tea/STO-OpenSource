using System;
using System.Collections.Generic;
using Opus.Engine.Input;
using Opus.Engine.Pal.Sdl3;
using Opus.Engine.Renderer.Direct3D12.Scene;

namespace Garupan.Garage.Demo;

/// <summary>Owns demo input subscriptions and per-frame derived controls.</summary>
internal sealed class DemoInputBindings : IDisposable
{
    private readonly GarageSceneController _garage;
    private readonly HashSet<Key> _pressed = new();
    private readonly SdlWindowService _window;
    private readonly int _windowWidth;
    private bool _disposed;
    private bool _dragging;

    public DemoInputBindings(
        SdlWindowService window,
        GarageSceneController garage,
        int windowWidth)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(garage);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowWidth);
        _window = window;
        _garage = garage;
        _windowWidth = windowWidth;

        _window.CloseRequested += OnCloseRequested;
        _window.KeyPressed += OnKeyPressed;
        _window.KeyReleased += OnKeyReleased;
        _window.MouseWheelScrolled += OnMouseWheelScrolled;
        _window.MouseButtonPressed += OnMouseButtonPressed;
        _window.MouseButtonReleased += OnMouseButtonReleased;
        _window.MouseMoved += OnMouseMoved;
    }

    public bool CloseRequested { get; private set; }

    public bool RestartRequested { get; private set; }

    public bool PauseToggleRequested { get; private set; }

    public float Throttle => AxisFromKeys(Key.W, Key.S);

    public float Steering => AxisFromKeys(Key.D, Key.A);

    public bool IsFireHeld => _pressed.Contains(Key.Space);

    public void ClearRestartRequest() => RestartRequested = false;

    public void ClearPauseToggleRequest() => PauseToggleRequested = false;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.CloseRequested -= OnCloseRequested;
        _window.KeyPressed -= OnKeyPressed;
        _window.KeyReleased -= OnKeyReleased;
        _window.MouseWheelScrolled -= OnMouseWheelScrolled;
        _window.MouseButtonPressed -= OnMouseButtonPressed;
        _window.MouseButtonReleased -= OnMouseButtonReleased;
        _window.MouseMoved -= OnMouseMoved;
        _pressed.Clear();
    }

    private float AxisFromKeys(Key positive, Key negative) =>
        (_pressed.Contains(positive) ? 1f : 0f)
        - (_pressed.Contains(negative) ? 1f : 0f);

    private void OnCloseRequested() => CloseRequested = true;

    private void OnKeyPressed(Key key)
    {
        _pressed.Add(key);
        switch (key)
        {
            case Key.Escape:
                CloseRequested = true;
                break;
            case Key.Up:
                _garage.Orbit.TogglePause();
                break;
            case Key.Left:
                _garage.Orbit.DecreaseSpeed();
                break;
            case Key.Right:
                _garage.Orbit.IncreaseSpeed();
                break;
            case Key.Down:
                _garage.Orbit.ResetSpeed();
                break;
            case Key.R:
                RestartRequested = true;
                break;
            case Key.P:
                PauseToggleRequested = true;
                break;
        }
    }

    private void OnKeyReleased(Key key) => _pressed.Remove(key);

    private void OnMouseWheelScrolled(float delta) => _garage.Zoom(delta);

    private void OnMouseButtonPressed(MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            _dragging = true;
        }
    }

    private void OnMouseButtonReleased(MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            _dragging = false;
        }
    }

    private void OnMouseMoved(int deltaX, int deltaY)
    {
        if (!_dragging)
        {
            return;
        }

        if (deltaX != 0)
        {
            _garage.Orbit.AdvancePhase(deltaX / (float)_windowWidth);
        }

        if (deltaY != 0)
        {
            _garage.PitchCamera(-deltaY);
        }
    }
}
