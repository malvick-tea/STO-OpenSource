using System.Collections.Generic;
using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Navigation;

/// <summary>
/// Manages active screens and transitions between them. Single-screen-active semantics:
/// only the top of the stack renders + receives <see cref="IScreen.Update"/>; lower
/// screens are paused until they become top again.
///
/// Transitions cross-fade the outgoing screen into the incoming one. During a transition
/// both screens still update (but only the incoming receives subsequent input) so visual
/// state is consistent.
/// </summary>
public sealed class ScreenStack
{
    /// <summary>
    /// Fraction of a transition spent fading the outgoing screen down to black; the
    /// remainder lifts the incoming screen back out of it. 0.5 keeps the fade symmetric.
    /// </summary>
    private const float FadeMidpoint = 0.5f;

    private readonly List<IScreen> _stack = new();
    private IScreen? _incoming;
    private ScreenTransition _transition = ScreenTransition.Instant;
    private float _transitionElapsed;
    private PendingMode _pendingMode;

    public IScreen? Current => _stack.Count == 0 ? null : _stack[_stack.Count - 1];

    public bool IsTransitioning => _pendingMode != PendingMode.None;

    public int Depth => _stack.Count;

    public void Replace(IScreen next, ScreenTransition transition)
    {
        Ensure.NotNull(next);
        BeginTransition(next, transition, PendingMode.Replace);
    }

    public void Push(IScreen next, ScreenTransition transition)
    {
        Ensure.NotNull(next);
        BeginTransition(next, transition, PendingMode.Push);
    }

    public void Pop(ScreenTransition transition)
    {
        if (_stack.Count == 0)
        {
            return;
        }

        _incoming = null;
        _pendingMode = PendingMode.Pop;
        _transition = transition;
        _transitionElapsed = 0f;

        if (transition.Kind == ScreenTransitionKind.Instant)
        {
            FinaliseTransition();
        }
    }

    /// <summary>
    /// Collapses the stack back down to the topmost screen of type
    /// <typeparamref name="TScreen"/>, discarding everything above it. Used to unwind a
    /// multi-screen sub-flow (briefing → match → result) in one step. The intermediate
    /// screens are exited immediately; the last one above the target is faded out via
    /// <paramref name="transition"/> so the reveal still animates.
    /// </summary>
    /// <returns><c>true</c> if a target screen was found and the stack collapsed;
    /// <c>false</c> if no such screen exists below the top (caller should fall back to a
    /// plain <see cref="Pop"/>).</returns>
    public bool PopTo<TScreen>(ScreenTransition transition)
        where TScreen : IScreen
    {
        if (_incoming is not null)
        {
            FinaliseTransition();
        }

        var targetIndex = -1;
        for (var i = _stack.Count - 1; i >= 0; i--)
        {
            if (_stack[i] is TScreen)
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex < 0 || targetIndex == _stack.Count - 1)
        {
            return false;
        }

        // Leave exactly one screen above the target so the trailing Pop can cross-fade
        // it into the revealed target; exit the rest now.
        while (_stack.Count > targetIndex + 2)
        {
            var top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            top.OnExit();
        }

        Pop(transition);
        return true;
    }

    public void Update(GameTime time, IInputSource input)
    {
        // During a transition the incoming screen receives input; otherwise the current
        // one does. This avoids double-handled clicks when the user lands on an entry
        // exactly as the transition completes.
        var inputTarget = _incoming ?? Current;
        if (inputTarget is not null)
        {
            inputTarget.Update(time, input);
        }

        // The non-input target still needs Update so animations keep ticking, but with a
        // null input source so it cannot react to user actions.
        if (_incoming is not null && Current is not null)
        {
            Current.Update(time, NullInput.Instance);
        }

        if (_pendingMode == PendingMode.None && _incoming is null)
        {
            return;
        }

        _transitionElapsed += (float)time.TickIntervalSeconds;
        if (_transition.IsDone(_transitionElapsed))
        {
            FinaliseTransition();
        }
    }

    public void Render(IDrawSurface surface)
    {
        if (_pendingMode == PendingMode.None)
        {
            Current?.Render(surface);
            return;
        }

        // Two-phase fade through black. The first half darkens the outgoing screen into a
        // black cover; the second half lifts the incoming screen (or, for a pop, the one
        // revealed beneath it) back out. Two opaque screens cannot be cross-dissolved on
        // this surface without per-screen alpha — render-to-texture lands with
        // Engine.Renderer in M3 — so fade-through-black is the honest transition the
        // immediate-mode surface can present, and the screen swap happens under full
        // black so there is no hard cut.
        var progress = _transition.SampleProgress(_transitionElapsed);
        if (progress < FadeMidpoint)
        {
            Current?.Render(surface);
            DrawBlackCover(surface, progress / FadeMidpoint);
        }
        else
        {
            (TransitionIncoming ?? Current)?.Render(surface);
            DrawBlackCover(surface, (1f - progress) / FadeMidpoint);
        }
    }

    /// <summary>
    /// The screen a live transition is moving toward: the queued screen for a push or
    /// replace, or the screen revealed beneath the top for a pop.
    /// </summary>
    private IScreen? TransitionIncoming =>
        _pendingMode == PendingMode.Pop
            ? (_stack.Count >= 2 ? _stack[_stack.Count - 2] : null)
            : _incoming;

    private static void DrawBlackCover(IDrawSurface surface, float strength)
    {
        var alpha = (byte)System.Math.Clamp((int)(strength * 255f), 0, 255);
        if (alpha > 0)
        {
            surface.FillRect(0, 0, surface.Width, surface.Height, new Color(0, 0, 0, alpha));
        }
    }

    private void BeginTransition(IScreen next, ScreenTransition transition, PendingMode mode)
    {
        if (_incoming is not null)
        {
            // Cancel in-flight transition: skip to its end so we have a clean state.
            FinaliseTransition();
        }

        _incoming = next;
        _pendingMode = mode;
        _transition = transition;
        _transitionElapsed = 0f;
        next.OnEnter();

        if (transition.Kind == ScreenTransitionKind.Instant)
        {
            FinaliseTransition();
        }
    }

    private void FinaliseTransition()
    {
        switch (_pendingMode)
        {
            case PendingMode.Replace:
                foreach (var s in _stack)
                {
                    s.OnExit();
                }

                _stack.Clear();
                if (_incoming is not null)
                {
                    _stack.Add(_incoming);
                }

                break;

            case PendingMode.Push:
                if (_incoming is not null)
                {
                    _stack.Add(_incoming);
                }

                break;

            case PendingMode.Pop:
                if (_stack.Count > 0)
                {
                    var top = _stack[_stack.Count - 1];
                    _stack.RemoveAt(_stack.Count - 1);
                    top.OnExit();
                }

                break;

            case PendingMode.None:
            default:
                break;
        }

        _incoming = null;
        _pendingMode = PendingMode.None;
        _transitionElapsed = 0f;
        _transition = ScreenTransition.Instant;
    }

    private enum PendingMode
    {
        None,
        Replace,
        Push,
        Pop,
    }
}
