using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Opus.Content.Meshes;
using Xunit;

namespace Garupan.Client.Windows.Direct3D12.Tests.Composition.Models;

/// <summary>Pins the contract between the procedural generator (tools/mapgen/build-japanese-city.py)
/// and the external PBR atlas loader: every material baked into the bundled <c>japan.glb</c> must be
/// named after a <c>materials.csv</c> id, uniquely, because <see cref="GltfImageReader.ReadMaterialBindings"/>
/// feeds those names straight into <c>ExternalMaterialAtlasBuilder</c>, which resolves
/// <c>textures/&lt;name&gt;/&lt;name&gt;_&lt;map&gt;.png</c>. An unnamed, duplicated, or off-manifest
/// material would silently mis-route — or alias — a texture set, so this is verified headlessly in CI
/// rather than left to a GPU run. The shipped map carries only the materials the layout actually
/// places (no swatch fence — see <c>--reference-fence</c>), so it is a strict subset of the manifest.</summary>
public sealed class JapanMapGlbTests
{
    private static readonly string[] RepresentativeMaterials =
    {
        "tower_glass_curtain",      // A downtown
        "mansion_concrete_balcony", // B mid-rise
        "shop_front_atlas",         // C shotengai
        "machiya_plaster",          // D machiya
        "kawara_roof_tile",
        "temple_wood_vermilion",    // E temple + park
        "asphalt_road",             // F infrastructure
        "sidewalk_concrete",
        "neon_sign_atlas",          // G signage
    };

    [Fact]
    public void Bundled_japan_glb_names_every_material_after_a_manifest_id()
    {
        var manifestIds = LoadManifestIds();
        var names = GltfImageReader.ReadMaterialBindings(File.ReadAllBytes(JapanGlbPath()))
            .Select(binding => binding.Name)
            .ToArray();

        names.Should().OnlyContain(name => !string.IsNullOrWhiteSpace(name), "the external loader keys textures by material name");
        names.Should().OnlyHaveUniqueItems("duplicate names would alias two materials onto one texture set");
        names.Should().BeSubsetOf(manifestIds, "every baked material must resolve to a manifest texture set");
        names.Should().Contain(RepresentativeMaterials, "every district must contribute its materials to the city");
    }

    [Fact]
    public void Bundled_japan_glb_omits_the_authoring_swatch_fence()
    {
        var manifestIds = LoadManifestIds();
        var names = GltfImageReader.ReadMaterialBindings(File.ReadAllBytes(JapanGlbPath()))
            .Select(binding => binding.Name)
            .ToArray();

        // The reference fence (off by default) is the only thing that would bake every manifest
        // material — including ground/prop-only ids — as an upright panel. Its absence is what keeps
        // sidewalks and asphalt from rendering as out-of-place vertical "buildings".
        names.Should().HaveCountLessThan(manifestIds.Length, "the shipped map drops the authoring swatch fence");
        names.Should().NotContain("foliage_atlas", "foliage is a prop-only material; nothing bakes it without the fence");
    }

    private static string[] LoadManifestIds()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "content", "maps", "japan", "materials.csv");
        File.Exists(path).Should().BeTrue("the test bundles the japan material manifest");
        return File.ReadLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Skip(1) // header row
            .Select(line => line.Split(',')[0])
            .ToArray();
    }

    private static string JapanGlbPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "content", "maps", "japan.glb");
        File.Exists(path).Should().BeTrue("the client bundles content/maps/japan.glb for the preferred battle map");
        return path;
    }
}
