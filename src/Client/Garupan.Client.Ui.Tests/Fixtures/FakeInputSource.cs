using System.Collections.Generic;
using Opus.Engine.Input;

namespace Garupan.Client.Ui.Tests.Fixtures;

/// <summary>
/// Hand-driven <see cref="IInputSource"/> for screen / adapter tests. <see cref="Hold"/>
/// marks keys as held this frame; <see cref="Press"/> additionally marks them as a rising
/// edge. Mouse buttons stay released — no test needs them yet. Fluent so a frame reads as
/// one expression: <c>new FakeInputSource().Hold(Key.W).Press(Key.Space)</c>.
/// </summary>
public sealed class FakeInputSource : IInputSource
{
    private readonly HashSet<Key> _down = new();
    private readonly HashSet<Key> _pressed = new();
    private readonly HashSet<MouseButton> _mouseDown = new();
    private readonly HashSet<MouseButton> _mousePressed = new();

    public (int X, int Y) MousePosition { get; set; }

    public (int X, int Y) MouseDelta { get; set; }

    public float MouseWheelDelta { get; set; }

    /// <summary>Marks <paramref name="keys"/> as held (level), without a rising edge.</summary>
    public FakeInputSource Hold(params Key[] keys)
    {
        foreach (var key in keys)
        {
            _down.Add(key);
        }

        return this;
    }

    /// <summary>Marks <paramref name="keys"/> as pressed this frame — both held and a
    /// rising edge, since a key that just went down is also down.</summary>
    public FakeInputSource Press(params Key[] keys)
    {
        foreach (var key in keys)
        {
            _down.Add(key);
            _pressed.Add(key);
        }

        return this;
    }

    /// <summary>Places the cursor at <paramref name="x"/>, <paramref name="y"/>. Returns
    /// the fixture for chaining.</summary>
    public FakeInputSource At(int x, int y)
    {
        MousePosition = (x, y);
        return this;
    }

    /// <summary>Supplies relative cursor movement for this frame.</summary>
    public FakeInputSource MoveMouse(int deltaX, int deltaY)
    {
        MouseDelta = (deltaX, deltaY);
        return this;
    }

    /// <summary>Supplies a vertical wheel delta for this frame.</summary>
    public FakeInputSource Scroll(float delta)
    {
        MouseWheelDelta = delta;
        return this;
    }

    /// <summary>Marks <paramref name="button"/> as held (level only).</summary>
    public FakeInputSource HoldMouse(MouseButton button)
    {
        _mouseDown.Add(button);
        return this;
    }

    /// <summary>Marks <paramref name="button"/> as pressed this frame — held + rising edge.</summary>
    public FakeInputSource PressMouse(MouseButton button)
    {
        _mouseDown.Add(button);
        _mousePressed.Add(button);
        return this;
    }

    public bool IsKeyDown(Key key) => _down.Contains(key);

    public bool IsKeyPressed(Key key) => _pressed.Contains(key);

    public bool IsMouseButtonDown(MouseButton button) => _mouseDown.Contains(button);

    public bool IsMouseButtonPressed(MouseButton button) => _mousePressed.Contains(button);
}
