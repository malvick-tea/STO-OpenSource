using Opus.Engine.Input;
using Opus.Engine.Ui;
using Opus.Foundation;

namespace Garupan.Client.Ui.Tests.Fixtures;

/// <summary>
/// No-op <see cref="IScreen"/> that records its lifecycle calls, for navigation tests.
/// Distinct subclasses give the type-keyed <c>ScreenStack.PopTo&lt;T&gt;</c> something to
/// match against.
/// </summary>
internal abstract class RecordingScreen : IScreen
{
    public int OnEnterCount { get; private set; }

    public int OnExitCount { get; private set; }

    public void OnEnter() => OnEnterCount++;

    public void OnExit() => OnExitCount++;

    public void Update(GameTime time, IInputSource input)
    {
    }

    public void Render(IDrawSurface surface)
    {
    }
}

/// <summary>The screen a sub-flow collapses back down to.</summary>
internal sealed class TargetScreen : RecordingScreen
{
}

/// <summary>A transient screen pushed above the target during a sub-flow.</summary>
internal sealed class FillerScreen : RecordingScreen
{
}
