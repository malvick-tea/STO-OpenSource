using System;
using FluentAssertions;
using Garupan.Tools.LocLint;
using Xunit;

namespace Garupan.Tools.Tests.LocLint;

/// <summary>Argument parsing for <c>loc-lint</c>. Unknown flags and missing values
/// surface as <see cref="ArgumentException"/> so the command can render usage; the
/// happy path threads each flag into its option slot.</summary>
public sealed class LocLintOptionsTests
{
    [Fact]
    public void Empty_args_returns_defaults()
    {
        var opts = LocLintOptions.Parse(System.Array.Empty<string>());

        opts.LocaleDirectory.Should().Be(LocLintOptions.DefaultLocaleDirectory);
        opts.DataDirectory.Should().Be(LocLintOptions.DefaultDataDirectory);
        opts.Locales.Should().Equal(LocLintOptions.DefaultLocales);
    }

    [Fact]
    public void LocaleDir_overrides_default()
    {
        var opts = LocLintOptions.Parse(new[] { "--locale-dir", "/tmp/loc" });

        opts.LocaleDirectory.Should().Be("/tmp/loc");
    }

    [Fact]
    public void DataDir_overrides_default()
    {
        var opts = LocLintOptions.Parse(new[] { "--data-dir", "/tmp/data" });

        opts.DataDirectory.Should().Be("/tmp/data");
    }

    [Fact]
    public void Locales_list_overrides_default()
    {
        var opts = LocLintOptions.Parse(new[] { "--locales", "en,fr" });

        opts.Locales.Should().Equal("en", "fr");
    }

    [Fact]
    public void Locales_list_trims_whitespace()
    {
        var opts = LocLintOptions.Parse(new[] { "--locales", "  en , ru , ja  " });

        opts.Locales.Should().Equal("en", "ru", "ja");
    }

    [Fact]
    public void Unknown_flag_throws_argument_exception()
    {
        var act = () => LocLintOptions.Parse(new[] { "--bogus" });

        act.Should().Throw<ArgumentException>().WithMessage("*--bogus*");
    }

    [Fact]
    public void Missing_value_for_flag_throws()
    {
        var act = () => LocLintOptions.Parse(new[] { "--locale-dir" });

        act.Should().Throw<ArgumentException>().WithMessage("*requires a value*");
    }

    [Fact]
    public void Empty_locale_list_throws()
    {
        var act = () => LocLintOptions.Parse(new[] { "--locales", "  " });

        act.Should().Throw<ArgumentException>().WithMessage("*at least one*");
    }
}
