using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Garupan.Server.Match;
using Microsoft.Extensions.Logging;

namespace Garupan.Server.Console;

internal sealed class ServerAdminConsoleController : IDisposable
{
    private const int MaximumTokenFileBytes = 4096;
    private const int MinimumTokenCharacters = 32;
    private const int MaximumCommandCharacters = 2048;
    private const int MaximumPendingCommands = 128;

    private readonly MatchHost _host;
    private readonly TextReader _input;
    private readonly ILogger _logger;
    private readonly byte[] _tokenHash;
    private readonly object _securitySync = new();
    private readonly ConcurrentQueue<uint> _kickQueue = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _readerTask;
    private int _pendingCommands;
    private int _disposed;

    private ServerAdminConsoleController(
        MatchHost host,
        TextReader input,
        ILogger logger,
        byte[] tokenHash)
    {
        _host = host;
        _input = input;
        _logger = logger;
        _tokenHash = tokenHash;
        _readerTask = Task.Run(ReadLoopAsync);
    }

    public static ServerAdminConsoleController Start(
        string tokenFilePath,
        MatchHost host,
        TextReader input,
        ILogger<ServerAdminConsoleController> logger)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(logger);
        return new ServerAdminConsoleController(
            host,
            input,
            logger,
            LoadTokenHash(tokenFilePath));
    }

    public void Drain()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        while (_kickQueue.TryDequeue(out var networkId))
        {
            Interlocked.Decrement(ref _pendingCommands);
            if (!_host.TryKickPlayer(networkId))
            {
                _logger.LogWarning(
                    "Admin kick ignored: network_id={NetworkId} is not connected.",
                    networkId);
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cancellation.Cancel();
        try
        {
            try
            {
                _readerTask.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException ex)
                when (ex.InnerExceptions.All(
                    static inner => inner is OperationCanceledException))
            {
            }
            catch (AggregateException ex)
            {
                _logger.LogWarning(
                    ex.Flatten(),
                    "Local admin command reader stopped with an error.");
            }
        }
        finally
        {
            lock (_securitySync)
            {
                CryptographicOperations.ZeroMemory(_tokenHash);
            }

            _cancellation.Dispose();
        }
    }

    internal static bool TryAuthenticateKick(
        string command,
        ReadOnlySpan<byte> expectedTokenHash,
        out uint networkId)
    {
        networkId = 0;
        if (command.Length is 0 or > MaximumCommandCharacters)
        {
            return false;
        }

        var fields = command.Split(
            ' ',
            count: 3,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length != 3
            || !string.Equals(fields[0], "kick", StringComparison.OrdinalIgnoreCase)
            || !uint.TryParse(
                fields[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out networkId))
        {
            return false;
        }

        var tokenBytes = Encoding.UTF8.GetBytes(fields[2]);
        try
        {
            Span<byte> candidateHash = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(tokenBytes, candidateHash);
            return expectedTokenHash.Length == candidateHash.Length
                && CryptographicOperations.FixedTimeEquals(
                    expectedTokenHash,
                    candidateHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tokenBytes);
        }
    }

    internal static byte[] LoadTokenHash(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fileBytes = ReadTokenFile(path);
        try
        {
            var token = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true)
                .GetString(fileBytes)
                .Trim();
            if (token.Length < MinimumTokenCharacters
                || token.Any(char.IsWhiteSpace)
                || token.Any(char.IsControl))
            {
                throw new InvalidDataException(
                    $"Admin token must contain at least {MinimumTokenCharacters} non-whitespace characters.");
            }

            var tokenBytes = Encoding.UTF8.GetBytes(token);
            try
            {
                return SHA256.HashData(tokenBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(tokenBytes);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(fileBytes);
        }
    }

    private static byte[] ReadTokenFile(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: MaximumTokenFileBytes,
            FileOptions.SequentialScan);
        if (stream.Length is <= 0 or > MaximumTokenFileBytes)
        {
            throw new InvalidDataException(
                $"Admin token file must contain between 1 and {MaximumTokenFileBytes} bytes.");
        }

        var fileBytes = new byte[checked((int)stream.Length)];
        try
        {
            stream.ReadExactly(fileBytes);
            if (stream.ReadByte() != -1)
            {
                throw new InvalidDataException(
                    $"Admin token file exceeds {MaximumTokenFileBytes} bytes.");
            }

            return fileBytes;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(fileBytes);
            throw;
        }
    }

    private async Task ReadLoopAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            string? command;
            try
            {
                command = await _input.ReadLineAsync(
                    _cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (command is null)
            {
                return;
            }

            uint networkId;
            lock (_securitySync)
            {
                if (!TryAuthenticateKick(command, _tokenHash, out networkId))
                {
                    _logger.LogWarning(
                        "Rejected malformed or unauthenticated local admin command.");
                    continue;
                }
            }

            if (Interlocked.Increment(ref _pendingCommands)
                > MaximumPendingCommands)
            {
                Interlocked.Decrement(ref _pendingCommands);
                _logger.LogWarning(
                    "Rejected local admin command because the bounded queue is full.");
                continue;
            }

            _kickQueue.Enqueue(networkId);
        }
    }
}
