using System.IO;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

/// <summary>
/// Spec coverage for <see cref="TankRosterCsv"/> — the data-driven roster loader that
/// replaced the hand-written C# constants. Proves id resolution against the shared
/// gun / mount / drive catalogues, optional-override layering, and strict validation.
/// </summary>
public sealed class TankRosterCsvTests
{
    private const string Header =
        "id,designation,display_name_key,model_res_path,school,crew_size,gun_id,reload_seconds," +
        "rounds_per_minute,penetration_mm,mount_id,drive_id,mass_tonnes,engine_hp,engine_torque_nm," +
        "body_length,body_width,body_height,turret_traverse_deg,hull_front_mm,hull_front_slope," +
        "hull_side_mm,hull_side_slope,hull_rear_mm,hull_rear_slope,turret_front_mm,turret_front_slope," +
        "turret_side_mm,turret_side_slope,turret_rear_mm,turret_rear_slope,mantlet_mm,mantlet_slope," +
        "roof_mm,roof_slope,rolling_resistance";

    private static string Csv(string row) => Header + "\n" + row;

    private static string Row(
        string id = "test_tank",
        string gunId = "gun_medium_a",
        string reload = "",
        string roundsPerMinute = "",
        string penetration = "",
        string mountId = "mount_medium_a",
        string driveId = "german_medium",
        string school = "PlayerSchool") =>
        $"{id},Test Tank,tank.test,res://x.glb,{school},5,{gunId},{reload},{roundsPerMinute}," +
        $"{penetration},{mountId},{driveId},23.6,300,1050,5.92,2.88,2.68,14," +
        "80,12,30,0,20,10,50,10,30,25,30,25,50,0,12,85,0.06";

    [Fact]
    public void Parse_resolves_shared_gun_mount_and_drive_by_id()
    {
        var tank = Single(TankRosterCsv.Parse(Csv(Row())));

        tank.Id.Should().Be("test_tank");
        tank.School.Should().Be(OpponentSchool.PlayerSchool);
        tank.Gun.Should().Be(GunCatalog.GunMediumA);
        tank.GunMount.Should().Be(GunMountCatalog.MountMediumA);
        tank.Mobility.Drive.Should().Be(GroundDriveCatalog.GermanMedium);
        tank.Armor.Should().Be(new ArmorProfile(
            HullFront: new ArmorPlate(80, 12),
            HullSide: new ArmorPlate(30, 0),
            HullRear: new ArmorPlate(20, 10),
            TurretFront: new ArmorPlate(50, 10),
            TurretSide: new ArmorPlate(30, 25),
            TurretRear: new ArmorPlate(30, 25),
            Mantlet: new ArmorPlate(50, 0),
            Roof: new ArmorPlate(12, 85)));
    }

    [Fact]
    public void Parse_layers_optional_gun_overrides_over_the_envelope()
    {
        var tank = Single(TankRosterCsv.Parse(Csv(Row(reload: "3.4", roundsPerMinute: "17", penetration: "99"))));

        tank.Gun.ReloadSeconds.Should().Be(3.4);
        tank.Gun.RoundsPerMinute.Should().Be(17);
        tank.Gun.PenetrationMm.Should().Be(99);
    }

    [Fact]
    public void Parse_empty_override_inherits_the_gun_catalogue_value()
    {
        var tank = Single(TankRosterCsv.Parse(Csv(Row())));

        tank.Gun.ReloadSeconds.Should().Be(GunCatalog.GunMediumA.ReloadSeconds);
        tank.Gun.PenetrationMm.Should().Be(GunCatalog.GunMediumA.PenetrationMm);
    }

    [Theory]
    [InlineData("gun_id", "no_such_gun", "mount_medium_a", "german_medium")]
    [InlineData("mount_id", "gun_medium_a", "no_such_mount", "german_medium")]
    [InlineData("drive_id", "gun_medium_a", "mount_medium_a", "no_such_drive")]
    public void Parse_rejects_unknown_catalogue_ids(string column, string gun, string mount, string drive)
    {
        var act = () => TankRosterCsv.Parse(Csv(Row(gunId: gun, mountId: mount, driveId: drive)));

        act.Should().Throw<InvalidDataException>().WithMessage($"*{column}*");
    }

    [Fact]
    public void Parse_rejects_unknown_school()
    {
        var act = () => TankRosterCsv.Parse(Csv(Row(school: "Atlantis")));

        act.Should().Throw<InvalidDataException>().WithMessage("*school*");
    }

    [Fact]
    public void Parse_rejects_duplicate_ids()
    {
        var act = () => TankRosterCsv.Parse(Csv(Row(id: "dup")) + "\n" + Row(id: "dup"));

        act.Should().Throw<InvalidDataException>().WithMessage("*appears more than once*");
    }

    [Fact]
    public void Parse_rejects_wrong_column_count()
    {
        var act = () => TankRosterCsv.Parse(Csv("too,few,columns"));

        act.Should().Throw<InvalidDataException>().WithMessage("*columns*");
    }

    [Fact]
    public void Parse_rejects_header_mismatch()
    {
        var act = () => TankRosterCsv.Parse("wrong,header\n" + Row());

        act.Should().Throw<InvalidDataException>().WithMessage("*header mismatch*");
    }

    [Fact]
    public void LoadFile_throws_for_missing_path()
    {
        var act = () => TankRosterCsv.LoadFile("nonexistent-roster.csv");

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Embedded_roster_loads_through_TankRoster_and_indexes_by_id()
    {
        TankRoster.All.Should().NotBeEmpty();
        TankRoster.FindById("vehicle_medium_a").Should().BeSameAs(TankRoster.VehicleMediumA);
        TankRoster.FindById("not_a_tank").Should().BeNull();
    }

    private static TankSpec Single(System.Collections.Generic.IReadOnlyList<TankSpec> tanks)
    {
        tanks.Should().HaveCount(1);
        return tanks[0];
    }
}
