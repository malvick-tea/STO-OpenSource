using FluentAssertions;
using Garupan.Localisation;
using Garupan.Tools.LocLint;
using Opus.Localisation;
using Xunit;

namespace Garupan.Tools.Tests.LocLint;

/// <summary>Reflection-based collection of <see cref="TranslationKey"/> constants from
/// a registry root + every nested type. Driven by a small in-test stub registry so the
/// scanner doesn't need <see cref="L10nKeys"/> stability to stay green.</summary>
public sealed class KeyRegistryScannerTests
{
    [Fact]
    public void Collect_walks_nested_types_and_returns_all_keys()
    {
        var keys = new KeyRegistryScanner().Collect(typeof(StubRegistry));

        keys.Should().BeEquivalentTo(new[]
        {
            "root.alpha",
            "root.beta",
            "nested.first",
            "nested.second",
            "deeper.payload",
        });
    }

    [Fact]
    public void Collect_ignores_non_TranslationKey_fields()
    {
        var keys = new KeyRegistryScanner().Collect(typeof(MixedRegistry));

        keys.Should().BeEquivalentTo(new[] { "mixed.real" });
    }

    [Fact]
    public void Collect_returns_empty_for_a_registry_with_no_keys()
    {
        var keys = new KeyRegistryScanner().Collect(typeof(EmptyRegistry));

        keys.Should().BeEmpty();
    }

    [Fact]
    public void Collect_against_real_L10nKeys_finds_canonical_keys()
    {
        var keys = new KeyRegistryScanner().Collect(typeof(L10nKeys));

        keys.Should().Contain("common.ok");
        keys.Should().Contain("menu.title");
        keys.Should().Contain("campaign.sample.name");
    }

    private static class StubRegistry
    {
        public static readonly TranslationKey Alpha = TranslationKey.Of("root.alpha");
        public static readonly TranslationKey Beta = TranslationKey.Of("root.beta");

        public static class Nested
        {
            public static readonly TranslationKey First = TranslationKey.Of("nested.first");
            public static readonly TranslationKey Second = TranslationKey.Of("nested.second");

            public static class Deeper
            {
                public static readonly TranslationKey Payload = TranslationKey.Of("deeper.payload");
            }
        }
    }

    private static class MixedRegistry
    {
        public static readonly TranslationKey RealKey = TranslationKey.Of("mixed.real");
        public static readonly string NotAKey = "not.a.translation.key";
        public static readonly int Number = 42;
    }

    private static class EmptyRegistry
    {
        // intentionally devoid of TranslationKey fields
    }
}
