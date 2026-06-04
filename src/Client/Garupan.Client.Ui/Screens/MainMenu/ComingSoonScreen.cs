using Garupan.Client.Ui.Navigation;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.MainMenu;

/// <summary>Generic "feature lands later" placeholder. Pops on Esc or click anywhere.
/// Used by <see cref="MainMenuActions"/> for nav entries whose real screen hasn't
/// shipped yet (PLAY / SETTINGS / ARCHIVE in Phase 0).</summary>
public sealed class ComingSoonScreen : IScreen
{
    private readonly ScreenStack _stack;
    private readonly string _label;

    public ComingSoonScreen(ScreenStack stack, string label)
    {
        _stack = stack;
        _label = label;
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
            _stack.Pop(ScreenTransition.Fade(0.2f));
        }
    }

    public void Render(IDrawSurface surface)
    {
        surface.Clear(MainMenuPalette.Background);
        var w = surface.Width;
        var h = surface.Height;

        var labelWidth = surface.MeasureText(_label, 64);
        surface.DrawText(_label, (w - labelWidth) / 2, (h / 2) - 48, 64, MainMenuPalette.Foreground);

        surface.FillRect((w - 240) / 2, h / 2 + 16, 240, 4, MainMenuPalette.Crimson);

        const string Sub = "coming soon";
        var subWidth = surface.MeasureText(Sub, 22);
        surface.DrawText(Sub, (w - subWidth) / 2, (h / 2) + 32, 22, MainMenuPalette.Dim);

        const string Hint = "Esc / click anywhere to go back.";
        var hintWidth = surface.MeasureText(Hint, 14);
        surface.DrawText(Hint, (w - hintWidth) / 2, h - 40, 14, MainMenuPalette.Dim);
    }
}
