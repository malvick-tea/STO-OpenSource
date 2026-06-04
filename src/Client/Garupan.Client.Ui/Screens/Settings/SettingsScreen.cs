using System;
using System.Linq;
using Garupan.Client.Core.Application;
using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Navigation;
using Garupan.Client.Ui.Screens.Settings.Multiplayer;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Settings;

/// <summary>
/// Real settings screen — video (vsync / resolution), audio (master / music / sfx
/// volumes), and language. Replaces the SETTINGS <c>ComingSoonScreen</c> placeholder.
/// Every change flows through <see cref="SettingsService"/> (persisted to
/// <c>user://settings.gsav</c>); volume changes reach the audio mixer live via the boot
/// AudioStage, locale changes re-activate the catalogue immediately. Video changes are
/// persisted and apply on the next launch (flagged "restart required" on the row).
///
/// This file is orchestration only: the option model lives in <see cref="SettingsModel"/>,
/// geometry in <see cref="SettingsLayout"/>, drawing in <see cref="SettingsScreenRenderer"/>.
/// </summary>
public sealed class SettingsScreen : IScreen
{
    private readonly ScreenStack _stack;
    private readonly LocalizationService _l10n;
    private readonly SettingsService _settings;
    private readonly SettingsModel _model;
    private readonly SettingsLayout _layout;
    private readonly SettingsScreenRenderer _renderer;

    private int _lastSurfaceWidth;

    public SettingsScreen(ScreenStack stack, LocalizationService l10n, SettingsService settings)
    {
        _stack = Ensure.NotNull(stack);
        _l10n = Ensure.NotNull(l10n);
        _settings = Ensure.NotNull(settings);

        var locales = _l10n.AvailableLocales.OrderBy(code => code, StringComparer.Ordinal).ToList();
        _model = new SettingsModel(_settings.Current, locales);
        _layout = new SettingsLayout(_model.Options);
        _renderer = new SettingsScreenRenderer(_l10n, _layout);
        _model.Changed += OnSettingsChanged;
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
        if (input.IsKeyPressed(Key.Escape))
        {
            _stack.Pop(ScreenTransition.Fade(0.25f));
            return;
        }

        HandleKeyboard(input);
        HandleMouse(input);
    }

    public void Render(IDrawSurface surface)
    {
        _lastSurfaceWidth = surface.Width;
        _renderer.Render(surface, _model);
    }

    private void HandleKeyboard(IInputSource input)
    {
        if (input.IsKeyPressed(Key.Up))
        {
            _model.MoveSelection(-1);
        }
        else if (input.IsKeyPressed(Key.Down))
        {
            _model.MoveSelection(+1);
        }

        if (input.IsKeyPressed(Key.Left))
        {
            _model.AdjustSelected(-1);
        }
        else if (input.IsKeyPressed(Key.Right))
        {
            _model.AdjustSelected(+1);
        }

        if (input.IsKeyPressed(Key.Enter) && _model.Options[_model.SelectedRow] is SettingsLinkOption link)
        {
            OpenLink(link.Target);
        }
    }

    private void HandleMouse(IInputSource input)
    {
        var (mx, my) = input.MousePosition;
        var row = _layout.RowOf(my);
        if (row < 0)
        {
            return;
        }

        _model.SelectRow(row);
        if (!input.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        if (_model.Options[row] is SettingsLinkOption link)
        {
            OpenLink(link.Target);
            return;
        }

        var direction = _layout.ArrowDirectionAt(mx, _lastSurfaceWidth);
        if (direction != 0)
        {
            _model.AdjustSelected(direction);
        }
    }

    private void OpenLink(SettingsLinkTarget target)
    {
        var screen = target switch
        {
            SettingsLinkTarget.Controls => (IScreen)new ControlsScreen(_stack, _l10n, _settings),
            SettingsLinkTarget.Multiplayer => new MultiplayerSettingsScreen(_stack, _l10n, _settings),
            _ => throw new InvalidOperationException($"Unhandled SettingsLinkTarget {target}"),
        };
        _stack.Push(screen, ScreenTransition.Fade(0.25f));
    }

    private void OnSettingsChanged(AppSettings next)
    {
        // SettingsModel never edits Bindings or Multiplayer — carry the live values
        // forward so a rebind (Controls sub-screen) or a server-address edit (Multiplayer
        // sub-screen) survives a later video / audio / language change.
        _settings.Apply(current => next with
        {
            Bindings = current.Bindings,
            Multiplayer = current.Multiplayer,
        });

        // SetLocale no-ops when the catalogue is unchanged, so the unconditional call is
        // safe and keeps the locale option's effect immediate.
        _l10n.SetLocale(next.Locale);
    }
}
