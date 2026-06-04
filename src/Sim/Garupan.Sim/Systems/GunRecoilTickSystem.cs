using Arch.Core;
using Garupan.Sim.Components;

namespace Garupan.Sim.Systems;

/// <summary>Returns recoiling gun assemblies to battery at their data-authored hydraulic
/// return speed. Fixed-step state keeps multiplayer snapshots and replays deterministic.</summary>
public sealed class GunRecoilTickSystem : IFixedSystem
{
    public string Name => "GunRecoilTick";

    public int Order => 440;

    public void Tick(in TickContext ctx)
    {
        var dt = MathF.Max(0f, (float)ctx.Time.TickIntervalSeconds);
        var query = new QueryDescription().WithAll<GunRecoilState>();
        ctx.World.Raw.Query(in query, (ref GunRecoilState recoil) =>
        {
            recoil.TravelMeters = MathF.Max(
                0f,
                recoil.TravelMeters - (recoil.ReturnSpeedMetersPerSecond * dt));
            if (recoil.TravelMeters == 0f)
            {
                recoil.ReturnSpeedMetersPerSecond = 0f;
            }
        });
    }
}
