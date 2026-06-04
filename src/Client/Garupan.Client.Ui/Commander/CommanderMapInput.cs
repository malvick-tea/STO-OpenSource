using Opus.Engine.Input;
using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Commander;

/// <summary>
/// Translates one frame of mouse input into operations on a <see cref="CommanderMapState"/>,
/// dispatching by the currently selected <see cref="CommanderTool"/>. Stateful — has to
/// remember whether the mouse button was held last frame because <see cref="IInputSource"/>
/// exposes "down" + "pressed" but not "released"; we synthesise the falling edge from the
/// difference.
///
/// Pencil / Marker (the stroke tools) share one state machine: press inside paper begins,
/// hold extends, release / leaving paper ends. Token uses a simpler one: a single press
/// inside paper places one token, no drag. Switching tools mid-stroke commits the active
/// stroke cleanly — the next press on the new tool starts fresh.
/// </summary>
public sealed class CommanderMapInput
{
    private bool _wasButtonDownLastFrame;
    private bool _isDrawing;
    private CommanderTool _toolUsedForActiveStroke;

    public void Update(
        IInputSource input,
        CommanderMapBounds paper,
        CommanderTool tool,
        Color inkColor,
        CommanderMapState state)
    {
        var (mouseX, mouseY) = input.MousePosition;
        var isButtonDown = input.IsMouseButtonDown(MouseButton.Left);
        var cursorInside = paper.Contains(mouseX, mouseY);
        var risingEdge = isButtonDown && !_wasButtonDownLastFrame;
        var fallingEdge = !isButtonDown && _wasButtonDownLastFrame;

        // Tool switched mid-stroke — terminate the in-flight stroke before dispatching.
        if (_isDrawing && tool != _toolUsedForActiveStroke)
        {
            state.End();
            _isDrawing = false;
        }

        switch (tool)
        {
            case CommanderTool.Pencil:
            case CommanderTool.Marker:
                UpdateStrokeTool(paper, mouseX, mouseY, isButtonDown, cursorInside, risingEdge, fallingEdge, tool, inkColor, state);
                break;

            case CommanderTool.Token:
                UpdateTokenTool(mouseX, mouseY, cursorInside, risingEdge, inkColor, state);
                break;
        }

        _wasButtonDownLastFrame = isButtonDown;
    }

    /// <summary>Resets the cached "was down" flag — useful when a screen is re-entered
    /// and the previous-frame state is stale.</summary>
    public void Reset()
    {
        _wasButtonDownLastFrame = false;
        _isDrawing = false;
    }

    private void UpdateStrokeTool(
        CommanderMapBounds paper,
        int mouseX, int mouseY,
        bool isButtonDown, bool cursorInside,
        bool risingEdge, bool fallingEdge,
        CommanderTool tool,
        Color inkColor,
        CommanderMapState state)
    {
        _ = paper;
        _ = isButtonDown;

        if (_isDrawing)
        {
            if (fallingEdge || !cursorInside)
            {
                state.End();
                _isDrawing = false;
            }
            else
            {
                state.Extend(new CommanderMapPoint(mouseX, mouseY));
            }
        }
        else if (risingEdge && cursorInside)
        {
            state.Begin(new CommanderMapPoint(mouseX, mouseY), inkColor, ThicknessFor(tool));
            _isDrawing = true;
            _toolUsedForActiveStroke = tool;
        }
    }

    private static void UpdateTokenTool(
        int mouseX, int mouseY,
        bool cursorInside, bool risingEdge,
        Color inkColor,
        CommanderMapState state)
    {
        // Single press inside paper places one token — no drag, no hold semantics. The
        // commander clicks once per token; misclicks are undone with Z.
        if (risingEdge && cursorInside)
        {
            var label = CommanderMapTokenLabels.Next(state);
            state.PlaceToken(new CommanderMapPoint(mouseX, mouseY), inkColor, label);
        }
    }

    private static int ThicknessFor(CommanderTool tool) => tool switch
    {
        CommanderTool.Marker => CommanderToolParameters.MarkerThicknessPixels,
        _                    => CommanderToolParameters.PencilThicknessPixels,
    };
}
