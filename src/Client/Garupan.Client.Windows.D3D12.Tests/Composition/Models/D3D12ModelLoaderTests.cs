using System;
using System.IO;
using FluentAssertions;
using Garupan.Client.Windows.Direct3D12.Composition.Models;
using Garupan.Client.Windows.Direct3D12.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Ui;
using Xunit;

namespace Garupan.Client.Windows.Direct3D12.Tests.Composition.Models;

/// <summary>GPU-touching tests for <see cref="D3D12ModelLoader"/>. The "missing path"
/// + "cache returns same instance" branches are CPU-only in spirit but the loader
/// requires a real <see cref="Opus.Engine.Rhi.Direct3D12.D3D12RhiDevice"/> at
/// construction (so the GPU-bound load branch can run on a real device when needed).
/// All tests acquire the shared session via <see cref="D3D12HostTestFixture"/> so the
/// device-creation cap stays at one for the whole assembly.</summary>
public sealed class D3D12ModelLoaderTests
{
    private const string TankVirtualPath = "res://tanks/vehicle_medium_b-rigged.glb";
    private const string TankRelativePath = "content/tanks/vehicle_medium_b-rigged.glb";
    private const string MissingPath = "res://missing/nope.glb";

    [SkippableFact]
    public void Missing_path_resolves_to_the_invalid_placeholder()
    {
        var session = D3D12HostTestFixture.TryAcquire();
        D3D12HostTestFixture.SkipIfUnavailable(session);

        var vfs = new FakeVfs();
        using var loader = new D3D12ModelLoader(session!.Device, vfs, NullLogger<D3D12ModelLoader>.Instance);

        var model = loader.Load(MissingPath);

        model.Should().BeSameAs(D3D12Model.Invalid);
        model.IsValid.Should().BeFalse();
    }

    [SkippableFact]
    public void Repeated_load_of_the_same_virtual_path_returns_the_cached_instance()
    {
        var session = D3D12HostTestFixture.TryAcquire();
        D3D12HostTestFixture.SkipIfUnavailable(session);

        var assetPath = Path.Combine(AppContext.BaseDirectory, TankRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Skip.IfNot(File.Exists(assetPath), $"Tank asset missing: {assetPath}");

        var vfs = new FakeVfs().Map(TankVirtualPath, assetPath);
        using var loader = new D3D12ModelLoader(session!.Device, vfs, NullLogger<D3D12ModelLoader>.Instance);

        var first = loader.Load(TankVirtualPath);
        var second = loader.Load(TankVirtualPath);

        first.IsValid.Should().BeTrue("the bundled tank glb is a valid asset");
        second.Should().BeSameAs(first, "the cache returns the same instance on the second load");
    }

    [SkippableFact]
    public void Load_after_dispose_throws_object_disposed_exception()
    {
        var session = D3D12HostTestFixture.TryAcquire();
        D3D12HostTestFixture.SkipIfUnavailable(session);

        var loader = new D3D12ModelLoader(session!.Device, new FakeVfs(), NullLogger<D3D12ModelLoader>.Instance);
        loader.Dispose();

        var act = () => loader.Load(MissingPath);
        act.Should().Throw<ObjectDisposedException>();
    }

    [SkippableFact]
    public void Loaded_tank_carries_a_multi_material_atlas_with_per_material_slots()
    {
        var session = D3D12HostTestFixture.TryAcquire();
        D3D12HostTestFixture.SkipIfUnavailable(session);

        var assetPath = Path.Combine(AppContext.BaseDirectory, TankRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Skip.IfNot(File.Exists(assetPath), $"Tank asset missing: {assetPath}");

        var vfs = new FakeVfs().Map(TankVirtualPath, assetPath);
        using var loader = new D3D12ModelLoader(session!.Device, vfs, NullLogger<D3D12ModelLoader>.Instance);

        var model = (D3D12Model)loader.Load(TankVirtualPath);
        var atlas = (MultiMaterialAtlas)model.Atlas!;

        atlas.MaterialCount.Should().Be(17, "the medium tank declares 17 materials in its glTF");
        atlas.UniqueImageCount.Should().Be(12, "the spec-gloss diffuse chain dedups to 12 distinct camo textures");
    }

    [SkippableFact]
    public void Invalid_placeholder_renders_as_no_op_through_the_imodel_contract()
    {
        var session = D3D12HostTestFixture.TryAcquire();
        D3D12HostTestFixture.SkipIfUnavailable(session);

        IModel model = D3D12Model.Invalid;

        model.IsValid.Should().BeFalse();
        model.BoundsMin.Should().Be(System.Numerics.Vector3.Zero);
        model.BoundsMax.Should().Be(System.Numerics.Vector3.Zero);

        var act = () => model.Dispose();
        act.Should().NotThrow("disposing the invalid singleton is safe — it owns no resources");
    }
}
