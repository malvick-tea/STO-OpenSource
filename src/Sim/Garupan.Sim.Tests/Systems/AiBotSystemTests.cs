using System.Numerics;
using FluentAssertions;
using Garupan.Sim;
using Garupan.Sim.Components;
using Garupan.Sim.Systems;
using Opus.Foundation;
using Xunit;

namespace Garupan.Sim.Tests.Systems;

public sealed class AiBotSystemTests
{
    [Fact]
    public void Bot_inside_engage_range_drives_and_aims_at_enemy()
    {
        using var world = World.Create();

        var bot = SpawnBot(world, position: Vector2.Zero, team: Team.OpponentSchool, engageRangeMeters: 100f);
        SpawnEnemy(world, position: new Vector2(50f, 0f), team: Team.PlayerSchool);

        new AiBotSystem().Tick(MakeCtx(world));

        var drive = world.Get<DriveInput>(bot);
        drive.Throttle.Should().BeGreaterThan(0f, "enemy is in range so the bot pursues");

        var aim = world.Get<TurretTarget>(bot);
        aim.YawRadians.Should().BeApproximately(0f, 0.001f, "enemy lies on +X");
    }

    [Fact]
    public void Bot_outside_engage_range_idles()
    {
        using var world = World.Create();

        var bot = SpawnBot(world, position: Vector2.Zero, team: Team.OpponentSchool, engageRangeMeters: 30f);
        SpawnEnemy(world, position: new Vector2(50f, 0f), team: Team.PlayerSchool); // 50 m > 30 m

        new AiBotSystem().Tick(MakeCtx(world));

        var drive = world.Get<DriveInput>(bot);
        drive.Throttle.Should().Be(0f, "enemy is outside engage range, bot must idle");
        drive.Steering.Should().Be(0f);
    }

    [Fact]
    public void Bot_ignores_friendly_units()
    {
        using var world = World.Create();

        var bot = SpawnBot(world, position: Vector2.Zero, team: Team.OpponentSchool, engageRangeMeters: 100f);
        SpawnEnemy(world, position: new Vector2(50f, 0f), team: Team.OpponentSchool); // same team

        new AiBotSystem().Tick(MakeCtx(world));

        world.Get<DriveInput>(bot).Throttle.Should().Be(0f, "no enemies on the field, bot idles");
    }

    [Fact]
    public void Bot_does_not_target_knocked_out_enemies()
    {
        using var world = World.Create();

        var bot = SpawnBot(world, position: Vector2.Zero, team: Team.OpponentSchool, engageRangeMeters: 100f);
        var enemy = SpawnEnemy(world, position: new Vector2(50f, 0f), team: Team.PlayerSchool);
        world.Add(enemy, default(KnockedOut));

        new AiBotSystem().Tick(MakeCtx(world));

        world.Get<DriveInput>(bot).Throttle.Should().Be(0f, "the only enemy is out, nothing to chase");
    }

    private static EntityHandle SpawnBot(World world, Vector2 position, Team team, float engageRangeMeters)
    {
        var bot = world.Spawn(
            new Transform(position, 0f),
            default(Hull),
            new Turret { YawRadians = 0f, TraverseSpeedRadPerS = 1f });
        world.Add(bot, new Gun { ReloadSeconds = 1f, ReloadSecondsMax = 5f });
        world.Add(bot, new TeamTag { Team = team });
        world.Add(bot, default(AiControlled));
        world.Add(bot, new BotBrain { EngageRangeMeters = engageRangeMeters });
        world.Add(bot, default(DriveInput));
        world.Add(bot, default(TurretTarget));
        return bot;
    }

    private static EntityHandle SpawnEnemy(World world, Vector2 position, Team team)
    {
        var e = world.Spawn(
            new Transform(position, 0f),
            default(Hull),
            new TeamTag { Team = team });
        return e;
    }

    private static TickContext MakeCtx(World world) =>
        new(world, GameTime.AtRate(60).Advance(), SimSeed.Zero, new CommandBuffer());
}
