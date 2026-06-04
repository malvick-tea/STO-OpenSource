using System;
using FluentAssertions;
using Garupan.Tools.ContentLint;
using Xunit;

namespace Garupan.Tools.Tests.ContentLint;

public sealed class ContentLintOptionsTests
{
    [Fact]
    public void Empty_args_returns_default_data_directory()
    {
        ContentLintOptions.Parse(Array.Empty<string>()).DataDirectory
            .Should().Be(ContentLintOptions.DefaultDataDirectory);
    }

    [Fact]
    public void Data_dir_flag_overrides_default()
    {
        ContentLintOptions.Parse(new[] { "--data-dir", "/tmp/data" }).DataDirectory
            .Should().Be("/tmp/data");
    }

    [Fact]
    public void Unknown_flag_throws()
    {
        var act = () => ContentLintOptions.Parse(new[] { "--bogus" });
        act.Should().Throw<ArgumentException>().WithMessage("*--bogus*");
    }

    [Fact]
    public void Missing_value_for_flag_throws()
    {
        var act = () => ContentLintOptions.Parse(new[] { "--data-dir" });
        act.Should().Throw<ArgumentException>().WithMessage("*requires a value*");
    }
}
