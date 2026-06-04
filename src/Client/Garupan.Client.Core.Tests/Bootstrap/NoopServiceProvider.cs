using System;

namespace Garupan.Client.Core.Tests.Bootstrap;

internal sealed class NoopServiceProvider : IServiceProvider
{
    public static readonly NoopServiceProvider Instance = new();

    public object? GetService(Type serviceType) => null;
}
