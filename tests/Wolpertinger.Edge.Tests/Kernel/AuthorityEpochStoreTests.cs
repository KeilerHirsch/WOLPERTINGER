using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Wolpertinger.Edge.Kernel;

namespace Wolpertinger.Edge.Tests.Kernel;

public sealed class AuthorityEpochStoreTests
{
    [Fact]
    public async Task NewStoreAllocatesMonotonicEpochsAndRestartContinues()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "control", "authority-epochs.bin");

        await using (var store = await AuthorityEpochStore.OpenAsync(path))
        {
            Assert.Equal(1UL, await store.NextAsync());
            Assert.Equal(2UL, await store.NextAsync());
        }

        await using var reopened = await AuthorityEpochStore.OpenAsync(path);
        Assert.Equal(3UL, await reopened.NextAsync());
    }

    [Fact]
    public async Task FirstRecordUsesBigEndianEpochAndDomainSeparatedSha256()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "control", "authority-epochs.bin");

        await using (var store = await AuthorityEpochStore.OpenAsync(path))
        {
            Assert.Equal(1UL, await store.NextAsync());
        }

        var record = await File.ReadAllBytesAsync(path);
        Assert.Equal(40, record.Length);
        Assert.Equal(1UL, BinaryPrimitives.ReadUInt64BigEndian(record.AsSpan(0, 8)));

        Span<byte> epochBytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(epochBytes, 1);
        var domain = Encoding.ASCII.GetBytes("WLEP-v1");
        var material = new byte[domain.Length + epochBytes.Length];
        domain.CopyTo(material, 0);
        epochBytes.CopyTo(material.AsSpan(domain.Length));
        var expectedDigest = SHA256.HashData(material);

        Assert.Equal(expectedDigest, record.AsSpan(8, 32).ToArray());
    }

    [Fact]
    public async Task TruncatedTailIsDiscardedAndNextEpochContinues()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "control", "authority-epochs.bin");

        await using (var store = await AuthorityEpochStore.OpenAsync(path))
        {
            Assert.Equal(1UL, await store.NextAsync());
            Assert.Equal(2UL, await store.NextAsync());
        }

        await using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None))
        {
            await stream.WriteAsync(new byte[] { 0xAA, 0xBB, 0xCC });
            stream.Flush(flushToDisk: true);
        }

        await using var reopened = await AuthorityEpochStore.OpenAsync(path);
        Assert.Equal(3UL, await reopened.NextAsync());
        Assert.Equal(120L, new FileInfo(path).Length);
    }

    [Fact]
    public async Task CorruptedCompletedRecordIsRejectedInsteadOfRepaired()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "control", "authority-epochs.bin");

        await using (var store = await AuthorityEpochStore.OpenAsync(path))
        {
            Assert.Equal(1UL, await store.NextAsync());
            Assert.Equal(2UL, await store.NextAsync());
        }

        var bytes = await File.ReadAllBytesAsync(path);
        bytes[40 + 12] ^= 0xFF;
        await File.WriteAllBytesAsync(path, bytes);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => AuthorityEpochStore.OpenAsync(path));
        Assert.Equal(80L, new FileInfo(path).Length);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "wolpertinger-authority-epoch-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
