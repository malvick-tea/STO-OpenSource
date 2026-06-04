using System;
using Microsoft.Extensions.DependencyInjection;

namespace Garupan.Client.Core.Composition;

/// <summary>
/// Process-lifetime DI container. Built once at boot, disposed on shutdown. Screens never
/// resolve from the root container directly — they receive their view-models and services
/// via constructor injection or the screen-stack factory.
/// </summary>
public sealed class ClientContainer : IDisposable
{
    private readonly ServiceProvider _root;

    private ClientContainer(ServiceProvider root)
    {
        _root = root;
    }

    public IServiceProvider Services => _root;

    public T Resolve<T>()
        where T : notnull
    {
        return _root.GetRequiredService<T>();
    }

    public T? ResolveOptional<T>()
        where T : class
    {
        return _root.GetService<T>();
    }

    public IServiceScope CreateScope() => _root.CreateScope();

    public void Dispose() => _root.Dispose();

    public static ClientContainer Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        CoreServicesModule.Register(services);
        configure(services);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        return new ClientContainer(provider);
    }
}
