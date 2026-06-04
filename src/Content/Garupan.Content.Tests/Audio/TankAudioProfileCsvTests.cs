using System.IO;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Audio;

/// <summary>
/// Spec coverage for <see cref="TankAudioProfileCsv"/> + the embedded <see cref="TankAudioCatalog"/>
/// — the data-driven sound paths that replaced hand-written C# audio constants.
/// </summary>
public sealed class TankAudioProfileCsvTests
{
    private const string Header =
        "tank_id,engine_start,engine_stop,engine_rev_up,engine_rev_down,engine_idle,engine_high," +
        "tracks,ground_effect,turret,gun,reload,is_default";

    private static string Csv(string row) => Header + "\n" + row;

    private static string Row(string id = "test_tank") =>
        $"{id},res://a/start.ogg,res://a/stop.ogg,res://a/revup.ogg,res://a/revdown.ogg," +
        "res://a/idle.ogg,res://a/high.ogg,res://a/tracks.ogg,res://a/ground.ogg," +
        "res://a/turret.ogg,res://a/gun.ogg,res://a/mg.ogg,true";

    [Fact]
    public void Parse_maps_the_eleven_path_columns_onto_the_profile()
    {
        var profiles = TankAudioProfileCsv.Parse(Csv(Row()));

        profiles.Should().ContainKey("test_tank");
        var profile = profiles["test_tank"];
        profile.EngineStartPath.Should().Be("res://a/start.ogg");
        profile.EngineHighPath.Should().Be("res://a/high.ogg");
        profile.GunPath.Should().Be("res://a/gun.ogg");
        profile.ReloadPath.Should().Be("res://a/mg.ogg");
        profile.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Parse_rejects_a_duplicate_tank_id()
    {
        var act = () => TankAudioProfileCsv.Parse(Csv(Row("dup")) + "\n" + Row("dup"));

        act.Should().Throw<InvalidDataException>().WithMessage("*appears more than once*");
    }

    [Fact]
    public void Parse_rejects_a_wrong_column_count()
    {
        var act = () => TankAudioProfileCsv.Parse(Csv("too,few,columns"));

        act.Should().Throw<InvalidDataException>().WithMessage("*columns*");
    }

    [Fact]
    public void Parse_rejects_an_empty_cell()
    {
        var act = () => TankAudioProfileCsv.Parse(Csv(Row().Replace("res://a/gun.ogg", string.Empty)));

        act.Should().Throw<InvalidDataException>().WithMessage("*gun*");
    }

    [Fact]
    public void Parse_rejects_a_header_mismatch()
    {
        var act = () => TankAudioProfileCsv.Parse("wrong,header\n" + Row());

        act.Should().Throw<InvalidDataException>().WithMessage("*header mismatch*");
    }

    [Fact]
    public void Parse_rejects_multiple_default_profiles()
    {
        var act = () => TankAudioProfileCsv.Parse(Csv(Row("first")) + "\n" + Row("second"));

        act.Should().Throw<InvalidDataException>().WithMessage("*exactly one profile as default*");
    }

    [Fact]
    public void LoadFile_throws_for_a_missing_path()
    {
        var act = () => TankAudioProfileCsv.LoadFile("nonexistent-audio.csv");

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Embedded_catalogue_resolves_the_player_tank_audio_profile()
    {
        var profile = TankAudioCatalog.RequireDefault();

        profile.GunPath.Should().Contain("cannon_75mm_kwk40");
        profile.ReloadPath.Should().Contain("reloading");
        profile.EngineStartPath.Should().Contain("engine_start");
        profile.Should().BeSameAs(TankAudioCatalog.RequireById(TankRoster.VehicleMediumB.Id));
        TankAudioCatalog.FindById("not_a_tank").Should().BeNull();
    }
}
