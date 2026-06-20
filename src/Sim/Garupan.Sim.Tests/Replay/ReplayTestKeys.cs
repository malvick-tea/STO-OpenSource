using System.Security.Cryptography;
using System.Text;

namespace Garupan.Sim.Tests.Replay;

internal static class ReplayTestKeys
{
    public static readonly byte[] IntegrityKey =
        SHA256.HashData(Encoding.UTF8.GetBytes("garupan-replay-tests"));
}
