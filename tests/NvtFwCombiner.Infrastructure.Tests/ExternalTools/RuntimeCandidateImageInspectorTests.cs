using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Header observations never confer compatibility or execute candidate code.</summary>
public sealed class RuntimeCandidateImageInspectorTests
{
    /// <summary>Native image facts retain architecture and characteristics, even for unsupported machines.</summary>
    [Theory]
    [InlineData(Machine.Amd64, PEMagic.PE32Plus, true)]
    [InlineData(Machine.I386, PEMagic.PE32, true)]
    [InlineData(Machine.Amd64, PEMagic.PE32Plus, false)]
    [InlineData(Machine.Unknown, PEMagic.PE32Plus, true)]
    public void NativeHeaderFactsPreserveObservedValues(Machine machine, PEMagic magic, bool dll)
    {
        byte[] bytes = CreateNativeImage(machine, magic, dll);
        ImmutableArray<byte> snapshot = [.. bytes];

        RuntimeCandidateImageInspection result = RuntimeCandidateImageInspector.Inspect(snapshot);

        Assert.False(result.IsMalformed);
        Assert.Equal(new RuntimeCandidateImageFacts(machine, dll, false, magic), result.Facts);
        Assert.Equal(bytes, snapshot.ToArray());
    }

    /// <summary>A real compiled managed DLL is observed as managed, not accepted as a native runtime.</summary>
    [Fact]
    public async Task ManagedHeaderPresenceIsReportedWithoutLoadingCandidate()
    {
        byte[] bytes = await File.ReadAllBytesAsync(typeof(RuntimeCandidateImageInspector).Assembly.Location,
            TestContext.Current.CancellationToken);

        RuntimeCandidateImageInspection result = RuntimeCandidateImageInspector.Inspect([.. bytes]);

        Assert.False(result.IsMalformed);
        Assert.True(result.Facts!.HasClrHeader);
        Assert.True(result.Facts.IsDll);
    }

    /// <summary>Truncated input yields a stable malformed observation rather than leaking parser errors.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(200)]
    public void TruncatedHeadersAreMalformed(int length)
    {
        byte[] bytes = CreateNativeImage(Machine.Amd64, PEMagic.PE32Plus, true);
        RuntimeCandidateImageInspection result = RuntimeCandidateImageInspector.Inspect([.. bytes.Take(length)]);
        Assert.True(result.IsMalformed);
        Assert.Null(result.Facts);
    }

    /// <summary>Default arrays and bad image signatures remain malformed without source mutation.</summary>
    [Fact]
    public void DefaultAndBadSignatureAreMalformed()
    {
        Assert.True(RuntimeCandidateImageInspector.Inspect(default).IsMalformed);
        byte[] bytes = CreateNativeImage(Machine.Amd64, PEMagic.PE32Plus, true);
        bytes[0x80] = 0;
        Assert.True(RuntimeCandidateImageInspector.Inspect([.. bytes]).IsMalformed);
    }

    private static byte[] CreateNativeImage(Machine machine, PEMagic magic, bool dll)
    {
        // Minimal PE/COFF fixture: one .text section; no loader or native execution is involved.
        byte[] bytes = new byte[1024];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0x5A4D);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3C), 0x80);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x80), 0x00004550);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x84), (ushort)machine);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x86), 1);
        ushort optionalSize = magic == PEMagic.PE32Plus ? (ushort)240 : (ushort)224;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x94), optionalSize);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x96), dll ? (ushort)0x2002 : (ushort)0x0002);
        Span<byte> optional = bytes.AsSpan(0x98);
        BinaryPrimitives.WriteUInt16LittleEndian(optional, (ushort)magic);
        BinaryPrimitives.WriteInt32LittleEndian(optional[32..], 4096);
        BinaryPrimitives.WriteInt32LittleEndian(optional[36..], 512);
        BinaryPrimitives.WriteInt32LittleEndian(optional[56..], 8192);
        BinaryPrimitives.WriteInt32LittleEndian(optional[60..], 512);
        BinaryPrimitives.WriteInt32LittleEndian(optional[(magic == PEMagic.PE32Plus ? 108 : 92)..], 16);
        Span<byte> section = bytes.AsSpan(0x98 + optionalSize);
        ".text"u8.CopyTo(section);
        BinaryPrimitives.WriteInt32LittleEndian(section[8..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(section[12..], 4096);
        BinaryPrimitives.WriteInt32LittleEndian(section[16..], 512);
        BinaryPrimitives.WriteInt32LittleEndian(section[20..], 512);
        bytes[512] = 0xC3;
        return bytes;
    }
}
