using System;
using System.Linq;
using Garupan.Client.Core.Application;
using Garupan.Client.Core.Services;
using Garupan.Client.Ui.Navigation;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Screens.Settings;

/// <summary>
/// Key-rebinding screen, pushed from <see cref="SettingsScreen"/>'s Controls row. Lists
/// the rebindable match actions; the player selects a row and presses a key, which the
/// <see cref="ControlsModel"/> binds (swapping on conflict). Changes flow straight to
/// <see cref="SettingsService"/> and persist to <c>user://settings.gsav</c>.
///
/// Orchestration only — the model lives in <see cref="ControlsModel"/>, geometry in
/// <see cref="ControlsLayout"/>, drawing in <see cref="ControlsRenderer"/>.
/// </summary>
public sealed class ControlsScreen : IScreen
{
    // Esc cancels a rebind and None is the null key — neither is a bindable target.
    private static readonly Key[] RebindableKeys =
        Enum.GetValues<Key>().Where(key => key != Key.None && key != Key.Escape).ToArray();

    private readonly ScreenStack _stack;
    private readonly SettingsService _settings;
    private readonly ControlsModel _model;
    private readonly ControlsLayout _layout;
    private readonly ControlsRenderer _renderer;

    public ControlsScreen(ScreenStack stack, LocalizationService l10n, SettingsService settings)
    {
        _stack = Ensure.NotNull(stack);
        Ensure.NotNull(l10n);
        _settings = Ensure.NotNull(settings);
        _model = new ControlsModel(_settings.Current.Bindings);
        _layout = new ControlsLayout(_model.Actions.Count);
        _renderer = new ControlsRenderer(l10n, _layout);
        _model.Changed += OnBindingsChanged;
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

        if (_model.IsListening)
        {
            UpdateListening(input);
            return;
        }

        if (input.IsKeyPressed(Key.Escape))
        {
            _stack.Pop(ScreenTransition.Fade(0.25f));
            return;
        }

        HandleNavigation(input);
    }

    public void Render(IDrawSurface surface) => _renderer.Render(surface, _model);

    private void UpdateListening(IInputSource input)
    {
        if (input.IsKeyPressed(Key.Escape))
        {
            _model.CancelRebind();
            return;
        }

        foreach (var key in RebindableKeys)
        {
            if (input.IsKeyPressed(key))
            {
                _model.CaptureKey(key);
                return;
            }
        }
    }

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

        if (input.IsKeyPressed(Key.Enter))
        {
            _model.BeginRebind();
        }

        var (_, mouseY) = input.MousePosition;
        var row = _layout.RowOf(mouseY);
        if (row < 0)
        {
            return;
        }

        _model.SelectRow(row);
        if (input.IsMouseButtonPressed(MouseButton.Left))
        {
            _model.BeginRebind();
        }
    }

    private void OnBindingsChanged(InputBindings bindings) =>
        _settings.Apply(settings => settings with { Bindings = bindings });
}
