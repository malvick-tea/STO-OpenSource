using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Garupan.Server.Console;
using Xunit;

namespace Garupan.Server.Console.Tests;

public sealed class ServerAdminConsoleControllerTests : IDisposable
{
    private const string Token = "0123456789abcdef0123456789abcdef";
    private readonly string _directory = Directory.CreateTempSubdirectory(
        "garupan-admin-token-tests-").FullName;

    [Fact]
    public void LoadTokenHash_and_command_authentication_accept_the_configured_token()
    {
        var path = Path.Combine(_directory, "admin.token");
        File.WriteAllText(path, Token);
        var tokenHash = ServerAdminConsoleController.LoadTokenHash(path);

        var accepted = ServerAdminConsoleController.TryAuthenticateKick(
            $"kick 42 {Token}",
            tokenHash,
            out var networkId);

        accepted.Should().BeTrue();
        networkId.Should().Be(42);
        CryptographicOperations.ZeroMemory(tokenHash);
    }

    [Theory]
    [InlineData("kick 42 wrong-token")]
    [InlineData("kick nope 0123456789abcdef0123456789abcdef")]
    [InlineData("status 42 0123456789abcdef0123456789abcdef")]
    public void Command_authentication_rejects_wrong_tokens_and_shapes(string command)
    {
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(Token));

        var accepted = ServerAdminConsoleController.TryAuthenticateKick(
            command,
            tokenHash,
            out _);

        accepted.Should().BeFalse();
        CryptographicOperations.ZeroMemory(tokenHash);
    }

    [Fact]
    public void LoadTokenHash_rejects_short_tokens()
    {
        var path = Path.Combine(_directory, "short.token");
        File.WriteAllText(path, "too-short");

        var act = () => ServerAdminConsoleController.LoadTokenHash(path);

        act.Should().Throw<InvalidDataException>();
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
