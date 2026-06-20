using System.Net;
using FluentAssertions;
using Garupan.Server.Console;
using Xunit;

namespace Garupan.Server.Console.Tests;

public sealed class ServerAddressAllowlistTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory(
        "garupan-allowlist-tests-").FullName;

    [Fact]
    public void Load_accepts_unique_ipv4_addresses_comments_and_blank_lines()
    {
        var path = Write(
            """
            # operations VPN
            127.0.0.1

            192.0.2.10
            127.0.0.1
            """);

        var addresses = ServerAddressAllowlist.Load(path);

        addresses.Should().BeEquivalentTo(
            IPAddress.Loopback,
            IPAddress.Parse("192.0.2.10"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("# comments only")]
    [InlineData("not-an-address")]
    [InlineData("::1")]
    public void Load_rejects_empty_or_invalid_lists(string content)
    {
        var path = Write(content);

        var act = () => ServerAddressAllowlist.Load(path);

        act.Should().Throw<InvalidDataException>();
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private string Write(string content)
    {
        var path = Path.Combine(_directory, "allowlist.txt");
        File.WriteAllText(path, content);
        return path;
    }
}
