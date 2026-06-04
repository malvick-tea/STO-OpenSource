using System;
using Garupan.Client.Core.Application;
using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Navigation;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Settings.Multiplayer;

/// <summary>
/// Local test server endpoint editor, pushed from <see cref="SettingsScreen"/>'s
/// Multiplayer row. The player edits a host string + a port; commits flow through
/// <see cref="SettingsService"/> and persist to <c>user://settings.gsav</c> (schema v3).
/// On Esc the current edits are committed (Enter is not required — the screen mirrors
/// the ControlsScreen convention where leaving the screen saves what was changed).
///
/// Orchestration only — the model lives in <see cref="MultiplayerSettingsModel"/>, the
/// geometry in <see cref="MultiplayerSettingsLayout"/>, drawing in
/// <see cref="MultiplayerSettingsRenderer"/>.
/// </summary>
public sealed class MultiplayerSettingsScreen : IScreen
{
    private static readonly Key[] PrintableKeys = BuildPrintableKeys();

    private readonly ScreenStack _stack;
    private readonly SettingsService _settings;
    private readonly MultiplayerSettingsModel _model;
    private readonly MultiplayerSettingsLayout _layout;
    private readonly MultiplayerSettingsRenderer _renderer;

    public MultiplayerSettingsScreen(ScreenStack stack, LocalizationService l10n, SettingsService settings)
    {
        _stack = Ensure.NotNull(stack);
        Ensure.NotNull(l10n);
        _settings = Ensure.NotNull(settings);
        _model = new MultiplayerSettingsModel(_settings.Current.Multiplayer);
        _layout = new MultiplayerSettingsLayout();
        _renderer = new MultiplayerSettingsRenderer(l10n, _layout);
        _model.Changed += OnMultiplayerChanged;
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
            _model.Commit();
            _stack.Pop(ScreenTransition.Fade(0.25f));
            return;
        }

        HandleNavigation(input);
        HandleEditing(input);
    }

    public void Render(IDrawSurface surface) => _renderer.Render(surface, _model);

    private void HandleNavigation(IInputSource input)
    {
        if (input.IsKeyPressed(Key.Up))
        {
            _model.MoveSelection(-1);
        }
        else if (input.IsKeyPressed(Key.Down))
        {
            _model.MoveSelection(+1);
        }
        else if (input.IsKeyPressed(Key.Left))
        {
            _model.MoveCursor(-1);
        }
        else if (input.IsKeyPressed(Key.Right))
        {
            _model.MoveCursor(+1);
        }
        else if (input.IsKeyPressed(Key.Enter))
        {
            _model.Commit();
        }

        var (_, mouseY) = input.MousePosition;
        var field = _layout.FieldOf(mouseY);
        if (field >= 0 && input.IsMouseButtonPressed(MouseButton.Left))
        {
            _model.SelectField(field);
        }
    }

    private void HandleEditing(IInputSource input)
    {
        if (input.IsKeyPressed(Key.Backspace))
        {
            _model.Backspace();
            return;
        }

        foreach (var key in PrintableKeys)
        {
            if (!input.IsKeyPressed(key))
            {
                continue;
            }

            var ch = KeyCharMap.ToPrintable(key);
            if (ch != '\0')
            {
                _model.Type(ch);
            }
        }
    }

    private void OnMultiplayerChanged(MultiplayerSettings next) =>
        _settings.Apply(settings => settings with { Multiplayer = next });

    private static Key[] BuildPrintableKeys()
    {
        var values = Enum.GetValues<Key>();
        var keys = new System.Collections.Generic.List<Key>(values.Length);
        foreach (var key in values)
        {
            if (KeyCharMap.ToPrintable(key) != '\0')
            {
                keys.Add(key);
            }
        }

        return keys.ToArray();
    }
}
