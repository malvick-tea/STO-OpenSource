using Garupan.Client.Ui.Navigation;
using Garupan.Client.Ui.Screens.MainMenu;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Campaign;

/// <summary>
/// Placeholder shown when the player presses DEPLOY on a mission briefing. The pre-v1.0
/// build does not ship the campaign match loop — campaigns are <b>v2.0 scope</b>, after
/// the multiplayer-first v1.0 ships (winter 2026 — early/mid spring 2027). Replaces the
/// throwaway top-down 2D match screen that was wired here as scaffolding.
///
/// Pops on Esc or any click. Esc handling is explicit (the engine's window service no
/// longer treats Esc as a quit-the-game key).
/// </summary>
public sealed class MissionInDevelopmentScreen : IScreen
{
    private readonly ScreenStack _stack;

    public MissionInDevelopmentScreen(ScreenStack stack)
    {
        _stack = Ensure.NotNull(stack);
    }

    public void OnEnter()
    {
    }

    public void OnExit()
    {
    }

    public void Update(GameTime time, IInputSource input)
    {
        _ = time;
        if (input.IsKeyPressed(Key.Escape) || input.IsMouseButtonPressed(MouseButton.Left))
        {
            _stack.Pop(ScreenTransition.Fade(0.25f));
        }
    }

    public void Render(IDrawSurface surface)
    {
        surface.Clear(MainMenuPalette.Background);
        var w = surface.Width;
        var h = surface.Height;

        const string Title = "CAMPAIGN";
        var titleWidth = surface.MeasureText(Title, 64);
        surface.DrawText(Title, (w - titleWidth) / 2, (h / 2) - 96, 64, MainMenuPalette.Foreground);

        surface.FillRect((w - 240) / 2, (h / 2) - 32, 240, 4, MainMenuPalette.Crimson);

        const string Sub = "in development";
        var subWidth = surface.MeasureText(Sub, 22);
        surface.DrawText(Sub, (w - subWidth) / 2, (h / 2) - 16, 22, MainMenuPalette.Dim);

        const string LineOne = "Campaigns ship in v2.0, after the v1.0 multiplayer release.";
        const string LineTwo = "Estimated window: winter 2026 — early / mid spring 2027.";
        var oneWidth = surface.MeasureText(LineOne, 16);
        var twoWidth = surface.MeasureText(LineTwo, 16);
        surface.DrawText(LineOne, (w - oneWidth) / 2, (h / 2) + 36, 16, MainMenuPalette.Foreground);
        surface.DrawText(LineTwo, (w - twoWidth) / 2, (h / 2) + 62, 16, MainMenuPalette.Dim);

        const string Hint = "Esc / click anywhere to go back.";
        var hintWidth = surface.MeasureText(Hint, 14);
        surface.DrawText(Hint, (w - hintWidth) / 2, h - 40, 14, MainMenuPalette.Dim);
    }
}
