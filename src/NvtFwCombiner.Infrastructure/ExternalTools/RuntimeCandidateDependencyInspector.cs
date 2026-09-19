using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

internal static class RuntimeCandidateDependencyInspector
{
    private const int MaximumEntries = 65_536;
    private const int MaximumStringBytes = 512;

    internal static string? Check(ImmutableArray<byte> executable, ImmutableArray<byte> runtime)
    {
        RuntimeCandidateImageFacts? toolFacts = RuntimeCandidateImageInspector.Inspect(executable).Facts;
        RuntimeCandidateImageFacts? runtimeFacts = RuntimeCandidateImageInspector.Inspect(runtime).Facts;
        if (toolFacts is null || runtimeFacts is null)
        {
            return "runtime.image.malformed";
        }
        if (toolFacts.Machine != Machine.Amd64 || toolFacts.Magic != PEMagic.PE32Plus ||
            runtimeFacts.Machine != Machine.Amd64 || runtimeFacts.Magic != PEMagic.PE32Plus ||
            !runtimeFacts.IsDll || runtimeFacts.HasClrHeader)
        {
            return "runtime.image.incompatible";
        }

        try
        {
            using PEReader tool = new(executable);
            using PEReader candidate = new(runtime);
            List<ImportedSymbol> required = ReadRequiredImports(tool);
            return required.Count == 0 ? "runtime.import.contract-missing" : CheckExports(candidate, required);
        }
        catch (Exception exception) when (exception is BadImageFormatException or
            ArgumentOutOfRangeException or OverflowException)
        {
            return "runtime.image.malformed";
        }
    }

    private static List<ImportedSymbol> ReadRequiredImports(PEReader image)
    {
        DirectoryEntry directory = image.PEHeaders.PEHeader!.ImportTableDirectory;
        List<ImportedSymbol> required = [];
        if (directory.RelativeVirtualAddress == 0 || directory.Size == 0)
        {
            return required;
        }
        int entries = Math.Min(directory.Size / 20, MaximumEntries);
        for (int index = 0; index < entries; index++)
        {
            BlobReader descriptor = image.GetSectionData(checked(directory.RelativeVirtualAddress + (index * 20))).GetReader(0, 20);
            uint originalThunk = descriptor.ReadUInt32();
            uint timestamp = descriptor.ReadUInt32();
            uint forwarder = descriptor.ReadUInt32();
            uint nameRva = descriptor.ReadUInt32();
            uint firstThunk = descriptor.ReadUInt32();
            if ((originalThunk | timestamp | forwarder | nameRva | firstThunk) == 0)
            {
                return required;
            }
            if (string.Equals(ReadAscii(image, nameRva), "VCRUNTIME140.dll", StringComparison.OrdinalIgnoreCase))
            {
                ReadThunks(image, originalThunk == 0 ? firstThunk : originalThunk, required);
            }
        }
        throw new BadImageFormatException("Import descriptors lack a bounded terminator.");
    }

    private static void ReadThunks(PEReader image, uint rva, List<ImportedSymbol> required)
    {
        for (int index = 0; index < MaximumEntries; index++)
        {
            ulong value = image.GetSectionData(checked((int)rva + (index * 8))).GetReader(0, 8).ReadUInt64();
            if (value == 0)
            {
                return;
            }
            if (required.Count >= MaximumEntries)
            {
                throw new BadImageFormatException("Too many imported symbols.");
            }
            if ((value & 0x8000000000000000) != 0)
            {
                if ((value & 0x7FFFFFFFFFFF0000) != 0)
                {
                    throw new BadImageFormatException("Invalid ordinal import.");
                }
                required.Add(new(null, (ushort)(value & 0xFFFF)));
            }
            else
            {
                required.Add(new(ReadAscii(image, checked((uint)value + 2)), null));
            }
        }
        throw new BadImageFormatException("Import thunks lack a bounded terminator.");
    }

    private static string? CheckExports(PEReader image, List<ImportedSymbol> required)
    {
        DirectoryEntry directory = image.PEHeaders.PEHeader!.ExportTableDirectory;
        if (directory.RelativeVirtualAddress == 0 || directory.Size < 40)
        {
            return "runtime.export.missing";
        }
        BlobReader table = image.GetSectionData(directory.RelativeVirtualAddress).GetReader(0, 40);
        table.Offset = 16;
        uint ordinalBase = table.ReadUInt32();
        uint functionCount = table.ReadUInt32();
        uint nameCount = table.ReadUInt32();
        uint functions = table.ReadUInt32();
        uint names = table.ReadUInt32();
        uint ordinals = table.ReadUInt32();
        if (functionCount > MaximumEntries || nameCount > MaximumEntries)
        {
            throw new BadImageFormatException("Export table exceeds its bounds.");
        }
        Dictionary<string, uint> indices = new(StringComparer.Ordinal);
        for (int index = 0; index < nameCount; index++)
        {
            uint nameRva = image.GetSectionData(checked((int)names + (index * 4))).GetReader(0, 4).ReadUInt32();
            ushort ordinalIndex = image.GetSectionData(checked((int)ordinals + (index * 2))).GetReader(0, 2).ReadUInt16();
            if (ordinalIndex >= functionCount || !indices.TryAdd(ReadAscii(image, nameRva), ordinalIndex))
            {
                throw new BadImageFormatException("Invalid export name/ordinal table.");
            }
        }
        foreach (ImportedSymbol symbol in required)
        {
            uint index;
            if (symbol.Name is { } name)
            {
                if (!indices.TryGetValue(name, out index))
                {
                    return "runtime.export.missing";
                }
            }
            else
            {
                uint ordinal = symbol.Ordinal!.Value;
                if (ordinal < ordinalBase || ordinal - ordinalBase >= functionCount)
                {
                    return "runtime.export.missing";
                }
                index = ordinal - ordinalBase;
            }
            uint functionRva = image.GetSectionData(checked((int)functions + ((int)index * 4))).GetReader(0, 4).ReadUInt32();
            if (functionRva == 0)
            {
                return "runtime.export.missing";
            }
            if (functionRva >= directory.RelativeVirtualAddress &&
                functionRva < checked(directory.RelativeVirtualAddress + directory.Size))
            {
                return "runtime.export.forwarder-unsupported";
            }
            _ = image.GetSectionData(checked((int)functionRva)).GetReader(0, 1).ReadByte();
        }
        return null;
    }

    private static string ReadAscii(PEReader image, uint rva)
    {
        PEMemoryBlock block = image.GetSectionData(checked((int)rva));
        BlobReader reader = block.GetReader(0, Math.Min(block.Length, MaximumStringBytes));
        List<byte> bytes = [];
        while (reader.RemainingBytes > 0)
        {
            byte value = reader.ReadByte();
            if (value == 0 && bytes.Count > 0)
            {
                return Encoding.ASCII.GetString([.. bytes]);
            }
            if (value is < 32 or > 126)
            {
                throw new BadImageFormatException("Invalid PE symbol string.");
            }
            bytes.Add(value);
        }
        throw new BadImageFormatException("PE string exceeds its bounds.");
    }

    private sealed record ImportedSymbol(string? Name, ushort? Ordinal);
}
