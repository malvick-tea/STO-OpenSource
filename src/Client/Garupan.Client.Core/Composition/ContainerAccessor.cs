namespace Garupan.Client.Core.Composition;

/// <summary>
/// Bridge for sites that cannot receive injection (UI scene factories, editor tooling,
/// hot-reload reentry). Set once during bootstrap; cleared on shutdown.
/// </summary>
/// <remarks>
/// This IS a service-locator. It's deliberate. Runtime code paths still receive their
/// collaborators via constructor injection — this exists so isolated scene previews and
/// crash-handler hooks have a way to reach the live container without restructuring all
/// of UI to take a <see cref="ClientContainer"/> through static fields.
/// </remarks>
public static class ContainerAccessor
{
    private static ClientContainer? _current;

    public static ClientContainer? Current => _current;

    public static void Set(ClientContainer container) => _current = container;

    public static void Clear() => _current = null;
}
