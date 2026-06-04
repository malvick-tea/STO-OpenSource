using Arch.Core;
using Garupan.Sim.Components;

namespace Garupan.Sim.Systems;

/// <summary>
/// Counts down each Gun's reload timer. KnockedOut tanks are skipped — the crew is
/// out of action so the cooldown freezes at whatever value it had at the moment of
/// the knock-out, which is both physically plausible and replay-safe.
///
/// Order: 450 — runs after turret aim, before gun fire.
/// Ported from <c>svo/engine/src/systems/reload_tick.cpp</c>.
/// </summary>
public sealed class ReloadTickSystem : IFixedSystem
{
    public string Name => "ReloadTick";

    public int Order => 450;

    public void Tick(in TickContext ctx)
    {
        var dt = (float)ctx.Time.TickIntervalSeconds;
        if (dt < 0f)
        {
            dt = 0f;
        }

        var query = new QueryDescription()
            .WithAll<Gun>()
            .WithNone<KnockedOut>();

        ctx.World.Raw.Query(in query, (ref Gun gun) =>
        {
            if (gun.ReloadSeconds <= 0f)
            {
                return; // already loaded
            }

            gun.ReloadSeconds -= dt;
            if (gun.ReloadSeconds < 0f)
            {
                gun.ReloadSeconds = 0f;
            }
        });
    }
}
