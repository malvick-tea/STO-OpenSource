using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Garupan.Server.Console;

internal static class ServerAddressAllowlist
{
    private const int MaximumFileBytes = 256 * 1024;
    private const int MaximumEntries = 4096;
    private const int MaximumLineLength = 256;

    public static IReadOnlyCollection<IPAddress> Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        if (stream.Length is <= 0 or > MaximumFileBytes)
        {
            throw new InvalidDataException(
                $"Server allowlist must contain between 1 and {MaximumFileBytes} bytes.");
        }

        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: false);
        var addresses = new HashSet<IPAddress>();
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Length > MaximumLineLength)
            {
                throw new InvalidDataException(
                    $"Server allowlist line {lineNumber} exceeds {MaximumLineLength} characters.");
            }

            var candidate = line.Trim();
            if (candidate.Length == 0 || candidate.StartsWith('#'))
            {
                continue;
            }

            if (!IPAddress.TryParse(candidate, out var address)
                || address.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new InvalidDataException(
                    $"Server allowlist line {lineNumber} is not a valid IPv4 address.");
            }

            addresses.Add(address);
            if (addresses.Count > MaximumEntries)
            {
                throw new InvalidDataException(
                    $"Server allowlist exceeds {MaximumEntries} unique addresses.");
            }
        }

        if (addresses.Count == 0)
        {
            throw new InvalidDataException(
                "Server allowlist does not contain any IPv4 addresses.");
        }

        return addresses;
    }
}
