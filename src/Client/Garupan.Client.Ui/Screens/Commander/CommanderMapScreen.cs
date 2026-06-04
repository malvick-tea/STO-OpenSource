using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Commander;
using Garupan.Client.Ui.Navigation;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Commander;

/// <summary>
/// Stand-alone screen wrapping the commander's hand-drawn map. Routed from the main menu
/// as the BATTLE PLAN entry so the tool can be exercised end-to-end before the tactical
/// 5v5 briefing flow exists to host it (per [[garupan-local test-2026]]).
///
/// This file is orchestration only — drawing lives in <see cref="CommanderMapRenderer"/>,
/// mouse → mark translation in <see cref="CommanderMapInput"/>, and layout maths in
/// <see cref="CommanderMapLayout"/>. Keyboard shortcuts are hard-coded here (not routed
/// through <c>InputBindings</c>) because the commander tool's keys are tool-internal,
/// not match controls — they don't belong in the player's match-key rebind panel.
/// </summary>
public sealed class CommanderMapScreen : IScreen
{
    private readonly ScreenStack _stack;
    private readonly LocalizationService _l10n;
    private readonly CommanderMapState _state = new();
    private readonly CommanderMapInput _input = new();
    private readonly CommanderMapRenderer _renderer = new();
    private CommanderTool _currentTool = CommanderTool.Pencil;
    private CommanderInk _selectedInk = CommanderInk.Primary;
    private int _lastSurfaceWidth;
    private int _lastSurfaceHeight;

    public CommanderMapScreen(ScreenStack stack, LocalizationService l10n)
    {
        _stack = Ensure.NotNull(stack);
        _l10n = Ensure.NotNull(l10n);
    }

    public CommanderTool CurrentTool => _currentTool;

    public CommanderInk SelectedInk => _selectedInk;

    public CommanderMapState State => _state;

    public void OnEnter() => _input.Reset();

    public void OnExit()
    {
    }

    public void Update(GameTime time, IInputSource input)
    {
        _ = time;

        if (HandleKeyboard(input))
        {
            return;
        }

        var paper = CommanderMapLayout.Paper(_lastSurfaceWidth, _lastSurfaceHeight);
        _input.Update(input, paper, _currentTool, CommanderInkColors.Of(_selectedInk), _state);
    }

    public void Render(IDrawSurface surface)
    {
        _lastSurfaceWidth = surface.Width;
        _lastSurfaceHeight = surface.Height;

        var paper = CommanderMapLayout.Paper(surface.Width, surface.Height);
        _renderer.Render(surface, paper, _state);
        DrawTopBar(surface);
        DrawToolbar(surface);
    }

    /// <summary>Routes keyboard commands. Returns true when a command was consumed —
    /// the caller skips the drawing pump for that frame, so a clear-and-keep-holding
    /// can't immediately start a new stroke from the cleared state.</summary>
    private bool HandleKeyboard(IInputSource input)
    {
        if (input.IsKeyPressed(Key.Escape))
        {
            _stack.Pop(ScreenTransition.Fade(0.25f));
            return true;
        }

        if (input.IsKeyPressed(Key.C))
        {
            _state.Clear();
            _input.Reset();
            return true;
        }

        if (input.IsKeyPressed(Key.Z))
        {
            _state.Undo();
            return true;
        }

        if (input.IsKeyPressed(Key.Q))
        {
            _currentTool = CommanderTool.Pencil;
            return true;
        }

        if (input.IsKeyPressed(Key.W))
        {
            _currentTool = CommanderTool.Marker;
            return true;
        }

        if (input.IsKeyPressed(Key.E))
        {
            _currentTool = CommanderTool.Token;
            return true;
        }

        if (input.IsKeyPressed(Key.Tab))
        {
            _selectedInk = _selectedInk == CommanderInk.Primary ? CommanderInk.Accent : CommanderInk.Primary;
            return true;
        }

        return false;
    }

    private void DrawTopBar(IDrawSurface surface)
    {
        var bar = CommanderMapLayout.TopBar(surface.Width);
        surface.FillRect(bar.X, bar.Y, bar.Width, bar.Height, CommanderMapPalette.HudPanel);
        surface.DrawText("BATTLE PLAN", 24, 14, 22, CommanderMapPalette.Foreground);
        surface.FillRect(bar.X, bar.Bottom - 2, bar.Width, 2, CommanderMapPalette.Border);
    }

    private void DrawToolbar(IDrawSurface surface)
    {
        var bar = CommanderMapLayout.Toolbar(surface.Width, surface.Height);
        surface.FillRect(bar.X, bar.Y, bar.Width, bar.Height, CommanderMapPalette.HudPanel);
        surface.FillRect(bar.X, bar.Y, bar.Width, 2, CommanderMapPalette.Border);

        DrawToolButton(surface, bar, CommanderTool.Pencil, slotIndex: 0, "Q");
        DrawToolButton(surface, bar, CommanderTool.Marker, slotIndex: 1, "W");
        DrawToolButton(surface, bar, CommanderTool.Token,  slotIndex: 2, "E");

        const int InkGroupOffset = 280;
        DrawInkSwatch(surface, bar, CommanderInk.Primary, slotIndex: 0, leftOffset: InkGroupOffset);
        DrawInkSwatch(surface, bar, CommanderInk.Accent,  slotIndex: 1, leftOffset: InkGroupOffset);

        const string Hint = "LMB use tool  •  Q pencil  •  W marker  •  E token  •  Tab ink  •  Z undo  •  C clear  •  Esc back";
        var hintWidth = surface.MeasureText(Hint, 13);
        surface.DrawText(Hint, surface.Width - hintWidth - 24, bar.Y + 22, 13, CommanderMapPalette.Dim);
    }

    private void DrawToolButton(IDrawSurface surface, CommanderMapBounds bar, CommanderTool tool, int slotIndex, string keyLabel)
    {
        const int ButtonSize = 40;
        const int ButtonGap = 12;
        var x = bar.X + 24 + (slotIndex * (ButtonSize + ButtonGap));
        var y = bar.Y + ((bar.Height - ButtonSize) / 2);
        var isSelected = _currentTool == tool;

        if (isSelected)
        {
            surface.FillRect(x - 3, y - 3, ButtonSize + 6, ButtonSize + 6, CommanderMapPalette.SelectionRing);
        }

        surface.FillRect(x, y, ButtonSize, ButtonSize, CommanderMapPalette.Paper);
        DrawToolGlyph(surface, tool, x + (ButtonSize / 2), y + (ButtonSize / 2));

        var labelWidth = surface.MeasureText(keyLabel, 11);
        surface.DrawText(keyLabel, x + ((ButtonSize - labelWidth) / 2), y + ButtonSize + 4, 11, CommanderMapPalette.Dim);

        _ = _l10n; // placeholder for future localised tool labels
    }

    private static void DrawToolGlyph(IDrawSurface surface, CommanderTool tool, int cx, int cy)
    {
        var ink = CommanderMapPalette.InkPrimary;
        switch (tool)
        {
            case CommanderTool.Pencil:
                // Thin diagonal stroke — pencil glyph.
                surface.DrawLine(cx - 10, cy + 8, cx + 10, cy - 8, 2, ink);
                break;

            case CommanderTool.Marker:
                // Thick diagonal stroke — marker glyph.
                surface.DrawLine(cx - 10, cy + 8, cx + 10, cy - 8, 7, ink);
                break;

            case CommanderTool.Token:
                // Token glyph — a small replica of the on-map token.
                surface.FillCircle(cx, cy, 10, CommanderMapPalette.Paper);
                surface.StrokeCircle(cx, cy, 10, 2, ink);
                surface.DrawText("1", cx - 3, cy - 7, 14, ink);
                break;
        }
    }

    private void DrawInkSwatch(IDrawSurface surface, CommanderMapBounds bar, CommanderInk ink, int slotIndex, int leftOffset)
    {
        const int SwatchSize = 36;
        const int SwatchGap = 12;
        var cx = bar.X + leftOffset + (slotIndex * (SwatchSize + SwatchGap)) + (SwatchSize / 2);
        var cy = bar.Y + (bar.Height / 2);
        var color = CommanderInkColors.Of(ink);

        if (_selectedInk == ink)
        {
            surface.StrokeCircle(cx, cy, (SwatchSize / 2) + 4, 2, CommanderMapPalette.SelectionRing);
        }

        surface.FillCircle(cx, cy, SwatchSize / 2, color);
    }
}
