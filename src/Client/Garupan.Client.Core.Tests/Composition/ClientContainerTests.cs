using System;
using FluentAssertions;
using Garupan.Client.Core.Bootstrap;
using Garupan.Client.Core.Composition;
using Garupan.Client.Core.Tests.Bootstrap;
using Garupan.Content;
using Microsoft.Extensions.DependencyInjection;
using Opus.Engine.Pal.Filesystem;
using Xunit;

namespace Garupan.Client.Core.Tests.Composition;

public sealed class ClientContainerTests
{
    [Fact]
    public void Build_registers_BootSequence()
    {
        using var c = ClientContainer.Build(WireFakeHost);

        c.ResolveOptional<BootSequence>().Should().NotBeNull();
    }

    [Fact]
    public void ContainerAccessor_round_trips_current_container()
    {
        using var c = ClientContainer.Build(WireFakeHost);
        ContainerAccessor.Set(c);

        ContainerAccessor.Current.Should().BeSameAs(c);

        ContainerAccessor.Clear();
        ContainerAccessor.Current.Should().BeNull();
    }

    [Fact]
    public void Configure_callback_sees_existing_registrations_and_can_extend()
    {
        using var c = ClientContainer.Build(services =>
        {
            WireFakeHost(services);
            services.AddSingleton<IDummy, Dummy>();
        });

        c.Resolve<IDummy>().Should().BeOfType<Dummy>();
    }

    /// <summary>
    /// Plugs in the platform contracts that <see cref="CoreServicesModule"/>'s services
    /// (SettingsService, LocalizationService, CampaignProgressService) need to construct
    /// under <c>ValidateOnBuild=true</c>. Real hosts register concrete impls here; tests
    /// use the no-op fakes from the Bootstrap namespace + a stub CampaignSpec.
    /// </summary>
    private static void WireFakeHost(IServiceCollection services)
    {
        services.AddSingleton<IVfs>(new NoopVfs());
        services.AddSingleton<CampaignSpec>(new CampaignSpec(
            Id: "test_campaign",
            NameKey: "campaign.test.name",
            ShortDescriptionKey: "campaign.test.subtitle",
            Missions: Array.Empty<MissionSpec>(),
            Nodes: Array.Empty<CampaignNode>()));
    }

    private interface IDummy
    {
    }

    private sealed class Dummy : IDummy
    {
    }
}
