using Opus.Engine.Input;

namespace Garupan.Client.Ui.Navigation;

/// <summary>
/// No-op input source. Handed to a screen during a transition when it's the outgoing
/// side: <see cref="IScreen.Update"/> keeps ticking for animation purposes, but the
/// screen can't react to user actions because every method returns the "not pressed /
/// not held" answer.
///
/// Singleton — there's no per-instance state, so every screen shares the same handle.
/// </summary>
internal sealed class NullInput : IInputSource
{
    public static readonly NullInput Instance = new();

    public (int X, int Y) MousePosition => (0, 0);

    public (int X, int Y) MouseDelta => (0, 0);

    public float MouseWheelDelta => 0f;

    public bool IsKeyDown(Key key) => false;

    public bool IsKeyPressed(Key key) => false;

    public bool IsMouseButtonDown(MouseButton button) => false;

    public bool IsMouseButtonPressed(MouseButton button) => false;
}
