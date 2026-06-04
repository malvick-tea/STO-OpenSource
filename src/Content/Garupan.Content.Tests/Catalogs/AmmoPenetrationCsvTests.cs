using System.IO;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

public sealed class AmmoPenetrationCsvTests
{
    private const string Header = "ammo_id,normal_100m_mm,normal_500m_mm,normal_1000m_mm";

    [Fact]
    public void Parse_reads_range_samples_and_catalogue_resolves_embedded_rounds()
    {
        var curves = AmmoPenetrationCsv.Parse(Header + "\ntest_round,135,123,109");

        curves.Should().ContainSingle();
        curves[0].AmmoId.Should().Be("test_round");
        curves[0].Normal100Mm.Should().Be(135);
        curves[0].Normal1000Mm.Should().Be(109);
        AmmoPenetrationCatalog.RequireById("ammo_medium_f_ap").Normal100Mm.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Parse_rejects_negative_penetration()
    {
        var act = () => AmmoPenetrationCsv.Parse(Header + "\ntest_round,135,-1,109");

        act.Should().Throw<InvalidDataException>().WithMessage("*normal_500m_mm*");
    }

    [Fact]
    public void Parse_rejects_duplicate_ammo_id()
    {
        var act = () => AmmoPenetrationCsv.Parse(Header + "\ndup,135,123,109\ndup,100,90,80");

        act.Should().Throw<InvalidDataException>().WithMessage("*duplicate*");
    }

    [Fact]
    public void Every_gun_default_round_has_a_penetration_table()
    {
        foreach (var gun in GunCatalog.All)
        {
            AmmoPenetrationCatalog.FindById(gun.DefaultAmmoId).Should().NotBeNull(
                $"gun \"{gun.Id}\" default round \"{gun.DefaultAmmoId}\" needs a penetration table to spawn");
        }
    }
}
