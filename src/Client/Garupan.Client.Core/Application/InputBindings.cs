using MemoryPack;
using Opus.Engine.Input;

namespace Garupan.Client.Core.Application;

/// <summary>
/// Player-rebindable keyboard controls for a match. Nested inside <see cref="AppSettings"/>
/// so it rides the same <c>user://settings.gsav</c> frame — input bindings are a user
/// preference like volume or locale, not a separate save kind. Turret aim stays on the
/// mouse and is not rebindable; menu / pause keys (Esc) are fixed by convention.
/// </summary>
/// <remarks>
/// Every property carries a default so the binary frame loads forward; the defaults are
/// the classic WASD + Space layout. Adding a binding here is a schema change — bump
/// <see cref="SaveSchemas.Settings"/>.
/// </remarks>
[MemoryPackable]
public sealed partial record InputBindings
{
    public Key MoveForward { get; init; } = Key.W;

    public Key MoveBackward { get; init; } = Key.S;

    public Key SteerLeft { get; init; } = Key.A;

    public Key SteerRight { get; init; } = Key.D;

    public Key Fire { get; init; } = Key.Space;

    public static InputBindings Default => new();
}
