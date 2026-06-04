using Garupan.Client.Ui.Navigation;
using Garupan.Content;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Garage;

/// <summary>
/// Stand-in for the real garage. Composes the 3D pedestal (<see cref="OrbitingTankView"/>),
/// the stat block (<see cref="TankStatBlockRenderer"/>), and the crew chips
/// (<see cref="PlayerCrewChipsRenderer"/>) on top of a 2D background.
///
/// This file is orchestration only — every <c>surface.*</c> call past the top bar lives
/// in one of the sub-renderers. The 3D pass happens between the top bar and the 2D
/// overlays so the chrome reads on top of the model.
/// </summary>
public sealed class GaragePlaceholderScreen : IScreen
{
    private const string TankModelPath = "res://tanks/vehicle_medium_b-rigged.glb";

    private readonly ScreenStack _stack;
    private readonly OrbitingTankView _tankView;
    private readonly TankStatBlockRenderer _statBlock = new();
    private readonly PlayerCrewChipsRenderer _crewChips;

    public GaragePlaceholderScreen(
        ScreenStack stack,
        IModelLoader modelLoader,
        IModelRenderer modelRenderer,
        CrewRoster crewRoster)
    {
        _stack = Ensure.NotNull(stack);
        Ensure.NotNull(modelLoader);
        Ensure.NotNull(modelRenderer);
        Ensure.NotNull(crewRoster);
        _tankView = new OrbitingTankView(modelLoader, modelRenderer, TankModelPath);
        _crewChips = new PlayerCrewChipsRenderer(crewRoster);
    }

    public void OnEnter() => _tankView.Load();

    public void OnExit()
    {
    }

    public void Update(GameTime time, IInputSource input)
    {
        _tankView.Tick(time.TickIntervalSeconds);
        if (input.IsKeyPressed(Key.Escape))
        {
            _stack.Pop(ScreenTransition.Fade(0.3f));
        }
    }

    public void Render(IDrawSurface surface)
    {
        surface.Clear(GaragePalette.Background);

        // Top bar.
        surface.FillRect(0, 0, surface.Width, 56, GaragePalette.Panel);
        surface.DrawText("GARAGE — the medium tank", 24, 14, 22, GaragePalette.Foreground);
        surface.FillRect(0, 56, surface.Width, 2, GaragePalette.Crimson);

        // 3D pedestal drawn before the 2D overlays so the chrome reads on top.
        _tankView.Render();

        _statBlock.Render(surface);
        _crewChips.Render(surface);

        DrawStatusHint(surface);
    }

    private void DrawStatusHint(IDrawSurface surface)
    {
        var loaded = _tankView.IsLoaded;
        var status = loaded
            ? "Esc to back out."
            : "MODEL FAILED TO LOAD — see logs. (Esc to back out.)";
        var color = loaded ? GaragePalette.Dim : GaragePalette.Crimson;
        var width = surface.MeasureText(status, 14);
        surface.DrawText(status, (surface.Width - width) / 2, surface.Height - 24, 14, color);
    }
}
