using System.Threading;
using System.Threading.Tasks;
using Garupan.Client.Core.Bootstrap;
using Garupan.Client.Ui.Navigation;
using Garupan.Client.Ui.Screens.Splash;

namespace Garupan.Client.Windows.Bootstrap;

/// <summary>
/// Splash-push stage. Runs at the bottom of the services band so that as soon as the
/// window is up there's something to look at. ScreenStack lives on the main thread, so
/// we marshal the mutation through the dispatcher.
/// </summary>
public sealed class SplashPushStage : IBootStage
{
    private readonly ScreenStack _stack;

    public SplashPushStage(ScreenStack stack)
    {
        _stack = stack;
    }

    public string Name => "SplashPush";

    public int Order => 110;

    public Task ExecuteAsync(BootContext ctx, CancellationToken ct) =>
        ctx.MainThread.InvokeAsync(() =>
            _stack.Replace(new SplashScreen(), ScreenTransition.Instant));
}
