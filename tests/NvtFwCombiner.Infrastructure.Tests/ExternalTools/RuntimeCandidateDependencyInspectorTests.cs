using System.Collections.Immutable;
using System.Buffers.Binary;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Dependency observations reject malformed images without loading native code.</summary>
public sealed class RuntimeCandidateDependencyInspectorTests
{
    /// <summary>Incomplete import/export images cannot establish compatibility.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    [InlineData(256)]
    public void MalformedImagesNeverEstablishRuntimeCompatibility(int length)
    {
        ImmutableArray<byte> bytes = ImmutableArray.Create(new byte[length]);
        Assert.Equal("runtime.image.malformed", RuntimeCandidateDependencyInspector.Check(bytes, bytes));
    }

    /// <summary>Name and ordinal imports are satisfied without rejecting an unrelated forwarded export.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequiredExportsResolveWithoutLoadingEitherImage(bool ordinal)
    {
        byte[] executable = Image(dll: false);
        byte[] runtime = Image(dll: true);
        if (ordinal)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(executable.AsSpan(0x250), 0x8000000000000001);
        }
        byte[] before = (byte[])runtime.Clone();
        Assert.Null(RuntimeCandidateDependencyInspector.Check([.. executable], [.. runtime]));
        Assert.Equal(before, runtime);
    }

    /// <summary>Absent, forwarded or unmapped required functions are never compatible.</summary>
    [Theory]
    [InlineData(0u, "runtime.export.missing")]
    [InlineData(0x1180u, "runtime.export.forwarder-unsupported")]
    [InlineData(0xFFFFFFu, "runtime.image.malformed")]
    public void RequiredExportMustResolveToMappedNonForwardedCode(uint functionRva, string issue)
    {
        byte[] runtime = Image(dll: true);
        Put32(runtime, 0x340, functionRva);
        Assert.Equal(issue, RuntimeCandidateDependencyInspector.Check([.. Image(false)], [.. runtime]));
    }

    /// <summary>Missing contracts and bounded malformed tables do not become empty successful comparisons.</summary>
    [Theory]
    [InlineData(0, "runtime.import.contract-missing")]
    [InlineData(1, "runtime.export.missing")]
    [InlineData(2, "runtime.image.malformed")]
    [InlineData(3, "runtime.export.missing")]
    [InlineData(4, "runtime.image.incompatible")]
    public void IncompleteContractsReject(int scenario, string expected)
    {
        byte[] executable = Image(false);
        byte[] runtime = Image(true);
        switch (scenario)
        {
            case 0: Put32(executable, 0x98 + 120, 0); break;
            case 1: "MissingSymbol\0"u8.CopyTo(runtime.AsSpan(0x360)); break;
            case 2: Put32(runtime, 0x314, 65537); break;
            case 3: BinaryPrimitives.WriteUInt64LittleEndian(executable.AsSpan(0x250), 0x8000000000000063); break;
            case 4: BinaryPrimitives.WriteUInt16LittleEndian(runtime.AsSpan(0x84), 0x14c); break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown fixture scenario.");
        }
        Assert.Equal(expected, RuntimeCandidateDependencyInspector.Check([.. executable], [.. runtime]));
    }

    private static byte[] Image(bool dll)
    {
        // Independent PE/COFF fixture: raw offset 0x200 maps to RVA 0x1000.
        byte[] bytes = new byte[2048];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0x5A4D);
        Put32(bytes, 0x3C, 0x80);
        Put32(bytes, 0x80, 0x4550);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x84), 0x8664);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x86), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x94), 240);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x96), dll ? (ushort)0x2002 : (ushort)2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x98), 0x20B);
        Put32(bytes, 0x98 + 32, 4096);
        Put32(bytes, 0x98 + 36, 512);
        Put32(bytes, 0x98 + 56, 8192);
        Put32(bytes, 0x98 + 60, 512);
        Put32(bytes, 0x98 + 108, 16);
        int section = 0x98 + 240;
        ".data"u8.CopyTo(bytes.AsSpan(section));
        Put32(bytes, section + 8, 1536);
        Put32(bytes, section + 12, 4096);
        Put32(bytes, section + 16, 1536);
        Put32(bytes, section + 20, 512);
        if (!dll)
        {
            Put32(bytes, 0x98 + 120, 0x1000);
            Put32(bytes, 0x98 + 124, 40);
            Put32(bytes, 0x200, 0x1050);
            Put32(bytes, 0x20C, 0x1030);
            "VCRUNTIME140.dll\0"u8.CopyTo(bytes.AsSpan(0x230));
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0x250), 0x1070);
            "RequiredSymbol\0"u8.CopyTo(bytes.AsSpan(0x272));
        }
        else
        {
            Put32(bytes, 0x98 + 112, 0x1100);
            Put32(bytes, 0x98 + 116, 0x100);
            Put32(bytes, 0x310, 1);
            Put32(bytes, 0x314, 2);
            Put32(bytes, 0x318, 2);
            Put32(bytes, 0x31C, 0x1140);
            Put32(bytes, 0x320, 0x1148);
            Put32(bytes, 0x324, 0x1150);
            Put32(bytes, 0x340, 0x1300);
            Put32(bytes, 0x344, 0x1180);
            Put32(bytes, 0x348, 0x1160);
            Put32(bytes, 0x34C, 0x1170);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x352), 1);
            "RequiredSymbol\0"u8.CopyTo(bytes.AsSpan(0x360));
            "OtherSymbol\0"u8.CopyTo(bytes.AsSpan(0x370));
            "ucrt.Other\0"u8.CopyTo(bytes.AsSpan(0x380));
            bytes[0x500] = 0xC3;
        }
        return bytes;
    }

    private static void Put32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    }
}
