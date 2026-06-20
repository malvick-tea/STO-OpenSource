using System.IO;
using FluentAssertions;
using Garupan.Content;
using Xunit;

namespace Garupan.Content.Tests.Catalogs;

/// <summary>Parser + catalog coverage for <see cref="ShellVisualCsv"/> /
/// <see cref="ShellVisualCatalog"/>.</summary>
public sealed class ShellVisualCsvTests
{
    private const string Header = "ammo_type,model_vfs_path,canon_source";

    [Fact]
    public void Parse_loads_a_single_row()
    {
        var csv = $"""
            {Header}
            AP,res://shell/test/scene.gltf,test author
            """;
        var catalog = ShellVisualCsv.Parse(csv);
        catalog.Count.Should().Be(1);
        var spec = catalog.Find(AmmoType.AP);
        spec.Should().NotBeNull();
        spec!.ModelVfsPath.Should().Be("res://shell/test/scene.gltf");
    }

    [Fact]
    public void Find_returns_null_for_ammo_without_a_binding()
    {
        var csv = $"""
            {Header}
            AP,res://shell/test/scene.gltf,test
            """;
        var catalog = ShellVisualCsv.Parse(csv);
        catalog.Find(AmmoType.HEAT).Should().BeNull();
        catalog.Contains(AmmoType.HEAT).Should().BeFalse();
    }

    [Fact]
    public void Parse_is_case_insensitive_on_ammo_type()
    {
        var csv = $"""
            {Header}
            apcr,res://shell/apcr/scene.gltf,test
            """;
        var catalog = ShellVisualCsv.Parse(csv);
        catalog.Find(AmmoType.APCR).Should().NotBeNull();
    }

    [Fact]
    public void Parse_supports_commas_in_canon_source_column()
    {
        var csv = $"""
            {Header}
            AP,res://shell/x/scene.gltf,multi-comma source, with extras, and more
            """;
        var catalog = ShellVisualCsv.Parse(csv);
        catalog.Count.Should().Be(1);
    }

    [Fact]
    public void Parse_throws_on_unknown_ammo_type()
    {
        var csv = $"""
            {Header}
            UltraPlasma,res://shell/futuristic/x.glb,not canon
            """;
        var act = () => ShellVisualCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*unknown ammo type*");
    }

    [Fact]
    public void Parse_throws_on_duplicate_ammo_rows()
    {
        var csv = $"""
            {Header}
            AP,res://shell/a/scene.gltf,first
            AP,res://shell/b/scene.gltf,duplicate
            """;
        var act = () => ShellVisualCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*more than once*");
    }

    [Fact]
    public void Parse_throws_on_empty_model_path()
    {
        var csv = $"""
            {Header}
            AP,,test
            """;
        var act = () => ShellVisualCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*model_vfs_path*empty*");
    }

    [Fact]
    public void Parse_throws_on_model_path_traversal()
    {
        var csv = $"""
            {Header}
            AP,res://shell/../outside.glb,test
            """;

        var act = () => ShellVisualCsv.Parse(csv);

        act.Should().Throw<InvalidDataException>().WithMessage("*model_vfs_path*unsafe*");
    }

    [Fact]
    public void Parse_throws_on_header_mismatch()
    {
        var csv = """
            type,path,source
            AP,res://x/x.glb,test
            """;
        var act = () => ShellVisualCsv.Parse(csv);
        act.Should().Throw<InvalidDataException>().WithMessage("*header mismatch*");
    }

    [Fact]
    public void Empty_catalog_has_no_entries()
    {
        ShellVisualCatalog.Empty.Count.Should().Be(0);
        ShellVisualCatalog.Empty.Find(AmmoType.AP).Should().BeNull();
    }

    [Fact]
    public void LoadFile_throws_FileNotFoundException_for_missing_path()
    {
        var act = () => ShellVisualCsv.LoadFile("nonexistent-shell-visuals.csv");
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Canonical_shell_visuals_csv_resolves_AP_round()
    {
        var canonicalPath = Path.Combine(
            System.AppContext.BaseDirectory, "data", "shell-visuals.csv");
        var catalog = ShellVisualCsv.LoadFile(canonicalPath);
        var ap = catalog.Find(AmmoType.AP);
        ap.Should().NotBeNull();
        ap!.ModelVfsPath.Should().Be("res://shell/pzgr39.glb");
    }
}
