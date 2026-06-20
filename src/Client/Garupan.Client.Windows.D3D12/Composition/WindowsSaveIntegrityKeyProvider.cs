using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Opus.Engine.Pal.Filesystem;
using Opus.Foundation.IO;
using Opus.Persistence;

namespace Garupan.Client.Windows.Direct3D12.Composition;

/// <summary>Persists one random save-integrity key protected by Windows DPAPI.</summary>
internal sealed class WindowsSaveIntegrityKeyProvider : ISaveIntegrityKeyProvider, IDisposable
{
    private const int KeyBytes = 32;
    private const int MaximumProtectedKeyBytes = 16 * 1024;
    private const uint CryptProtectUiForbidden = 0x1;
    private const string KeyFileName = "save-integrity.dpapi";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _root;
    private readonly string _path;
    private byte[]? _cachedKey;
    private bool _disposed;

    public WindowsSaveIntegrityKeyProvider(IVfs vfs)
    {
        ArgumentNullException.ThrowIfNull(vfs);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows DPAPI save-integrity protection requires Windows.");
        }

        _root = vfs.Realize("user://");
        _path = PathContainment.ResolveUnderRoot(_root, KeyFileName);
    }

    public async ValueTask<ReadOnlyMemory<byte>> GetKeyAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_cachedKey is null)
            {
                _cachedKey = LoadOrCreate(cancellationToken);
            }

            return _cachedKey;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Wait();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_cachedKey is not null)
            {
                CryptographicOperations.ZeroMemory(_cachedKey);
                _cachedKey = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private byte[] LoadOrCreate(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(_path))
        {
            PathContainment.RejectReparsePoints(_root, _path);
            var protectedKey = ReadProtectedKey();
            try
            {
                return Unprotect(protectedKey);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(protectedKey);
            }
        }

        Directory.CreateDirectory(_root);
        PathContainment.RejectReparsePoints(_root, _root);
        var key = RandomNumberGenerator.GetBytes(KeyBytes);
        try
        {
            var protectedKey = Protect(key);
            try
            {
                WriteNewKeyAtomically(protectedKey, cancellationToken);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(protectedKey);
            }

            if (File.Exists(_path))
            {
                PathContainment.RejectReparsePoints(_root, _path);
                var persistedProtectedKey = ReadProtectedKey();
                try
                {
                    var persisted = Unprotect(persistedProtectedKey);
                    CryptographicOperations.ZeroMemory(key);
                    return persisted;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(persistedProtectedKey);
                }
            }

            throw new IOException("Save-integrity key was not persisted.");
        }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            throw;
        }
    }

    private byte[] ReadProtectedKey()
    {
        using var stream = new FileStream(
            _path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        if (stream.Length is <= 0 or > MaximumProtectedKeyBytes)
        {
            throw new InvalidDataException(
                "The persisted save-integrity key has an invalid protected size.");
        }

        var protectedKey = new byte[checked((int)stream.Length)];
        try
        {
            stream.ReadExactly(protectedKey);
            if (stream.ReadByte() == -1)
            {
                return protectedKey;
            }

            throw new InvalidDataException(
                "The persisted save-integrity key changed while it was being read.");
        }
        catch
        {
            CryptographicOperations.ZeroMemory(protectedKey);
            throw;
        }
    }

    private void WriteNewKeyAtomically(
        byte[] protectedKey,
        CancellationToken cancellationToken)
    {
        var temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(protectedKey);
                stream.Flush(flushToDisk: true);
            }

            try
            {
                File.Move(temporaryPath, _path, overwrite: false);
            }
            catch (IOException) when (File.Exists(_path))
            {
                File.Delete(temporaryPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static byte[] Protect(byte[] plaintext) =>
        TransformWithDpapi(plaintext, protect: true);

    private static byte[] Unprotect(byte[] protectedBytes)
    {
        var plaintext = TransformWithDpapi(protectedBytes, protect: false);
        if (plaintext.Length != KeyBytes)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new CryptographicException(
                "The persisted save-integrity key has an invalid length.");
        }

        return plaintext;
    }

    private static byte[] TransformWithDpapi(byte[] input, bool protect)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Length is <= 0 or > MaximumProtectedKeyBytes)
        {
            throw new CryptographicException(
                "DPAPI save-integrity input exceeds the configured size limit.");
        }

        var inputPointer = Marshal.AllocHGlobal(input.Length);
        try
        {
            Marshal.Copy(input, 0, inputPointer, input.Length);
            var inputBlob = new DataBlob(input.Length, inputPointer);
            DataBlob outputBlob;
            var succeeded = protect
                ? CryptProtectData(
                    ref inputBlob,
                    "STO save integrity",
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out outputBlob)
                : CryptUnprotectData(
                    ref inputBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out outputBlob);
            if (!succeeded)
            {
                throw new CryptographicException(Marshal.GetLastWin32Error());
            }

            try
            {
                if (outputBlob.Size is <= 0 or > MaximumProtectedKeyBytes)
                {
                    throw new CryptographicException(
                        "DPAPI save-integrity output exceeds the configured size limit.");
                }

                var output = new byte[outputBlob.Size];
                Marshal.Copy(outputBlob.Data, output, 0, output.Length);
                return output;
            }
            finally
            {
                if (outputBlob.Data != IntPtr.Zero)
                {
                    LocalFree(outputBlob.Data);
                }
            }
        }
        finally
        {
            Marshal.Copy(new byte[input.Length], 0, inputPointer, input.Length);
            Marshal.FreeHGlobal(inputPointer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public DataBlob(int size, IntPtr data)
        {
            Size = size;
            Data = data;
        }

        public int Size;

        public IntPtr Data;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr prompt,
        uint flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr prompt,
        uint flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
