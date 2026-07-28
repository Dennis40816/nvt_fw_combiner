using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Application-owned evidenced TP header layouts used for semantic report classification.</summary>
public static partial class TpHeaderCatalog
{
    private static readonly Dictionary<string, TpHeaderLayout> HeaderLayoutsByIc = BuildHeaderLayouts();

    /// <summary>Returns the header layout assigned to an IC.</summary>
    public static bool TryGetLayout(string icId, out TpHeaderLayout? layout)
    {
        return HeaderLayoutsByIc.TryGetValue(icId, out layout);
    }

    private static Dictionary<string, TpHeaderLayout> BuildHeaderLayouts()
    {
        TpHeaderLayout normal920And923 = CreateNormal920And923Layout();
        TpHeaderLayout normal925And926 = CreateNormal925And926Layout();
        TpHeaderLayout nt51927 = CreateNt51927Layout();
        TpHeaderLayout nt51932 = CreateNt51932TypeAbLayout();
        TpHeaderLayout nt51950 = CreateNt51950Layout();

        return new Dictionary<string, TpHeaderLayout>(StringComparer.Ordinal)
        {
            ["NT51917"] = Alias("nt51917-header-927", "NT51917 TP header (NT51927 alias)", nt51927, "TP Overview owner confirmation: NT51917 follows NT51927."),
            ["NT51919"] = Alias("nt51919-header-932-type-ab", "NT51919 TP header (NT51932 Type A/B alias)", nt51932, "TP Overview owner confirmation chain: NT51919 follows NT51929; NT51929 follows NT51932 postbuild."),
            ["NT51923"] = Alias("nt51923-header-920-923", "NT51923 normal TP header", normal920And923, normal920And923.Evidence, TpHeaderModelStatus.Workbook),
            ["NT51926"] = Alias("nt51926-header-925-926", "NT51926 normal TP header", normal925And926, normal925And926.Evidence, TpHeaderModelStatus.Workbook),
            ["NT51927"] = nt51927,
            ["NT51928"] = Alias("nt51928-header-927", "NT51928 TP header (non-NB NT51927 alias)", nt51927, "TP Overview evidence: NT51928 non-NB follows NT51927. NB is intentionally not modeled."),
            ["NT51929"] = Alias("nt51929-header-932-type-ab", "NT51929 TP header (NT51932 Type A/B alias)", nt51932, "TP Overview owner confirmation: NT51929 follows NT51932 postbuild."),
            ["NT51932"] = nt51932,
            ["NT51950"] = nt51950,
            ["NT51951"] = Alias("nt51951-header-950", "NT51951 TP header (NT51950 postbuild alias)", nt51950, "TP Overview owner confirmation: NT51951 follows NT51950 postbuild."),
        };
    }

    private static TpHeaderLayout Alias(
        string layoutId,
        string displayName,
        TpHeaderLayout source,
        string evidence,
        TpHeaderModelStatus status = TpHeaderModelStatus.DocumentedAlias)
    {
        return new TpHeaderLayout(layoutId, displayName, status, source.Ranges, source.Fields, evidence);
    }

    private static TpHeaderLayout CreateNormal920And923Layout()
    {
        return new TpHeaderLayout(
            "normal-920-923",
            "920/923 normal TP header",
            TpHeaderModelStatus.Workbook,
            [new ByteRange(0x0000, 0x0100)],
            CreateNormalHeaderFields(
                Field("same-code", "Same code", 0x20, 1),
                Field("spi-option", "SPI option", 0x21, 3)),
            "TDDI_Flash_Header.xlsx worksheet '920&923'.");
    }

    private static TpHeaderLayout CreateNormal925And926Layout()
    {
        return new TpHeaderLayout(
            "normal-925-926",
            "925/926 normal TP header",
            TpHeaderModelStatus.Workbook,
            [new ByteRange(0x0000, 0x0100)],
            CreateNormalHeaderFields(
                Field("cascade-info", "Cascade info", 0x20, 1),
                Field("spi-option", "SPI option", 0x21, 1),
                Field("t6-t4", "T6/T4", 0x22, 2)),
            "TDDI_Flash_Header.xlsx worksheet '925&926'.");
    }

    private static List<TpHeaderField> CreateNormalHeaderFields(params TpHeaderField[] descriptor20Fields)
    {
        List<TpHeaderField> fields =
        [
            Word("ilm-start-address-in-bin", "ILM start address in BIN", 0x00),
            Word("ilm-destination-address-in-sram", "ILM destination address in SRAM", 0x04),
            Word("ilm-size", "ILM size", 0x08),
            Word("data-start-address-in-bin", "DATA start address in BIN", 0x0C),
            Word("data-destination-address-in-sram", "DATA destination address in SRAM", 0x10),
            Word("data-size", "DATA size", 0x14),
            Word("ilm-crc-0", "ILM CRC 0", 0x18),
            Word("dlm-crc-0", "DLM CRC 0", 0x1C),
            .. descriptor20Fields,
            Word("svn-auto-build-version", "SVN auto-build version", 0x24),
            Word("ov-info", "OV info", 0x28),
            Word("fw-config-destination-address-in-sram", "FW Config destination address in SRAM", 0x30),
            Word("fw-config-start-address-in-bin", "FW Config start address in BIN", 0x38),
            Word("fw-config-crc", "FW Config CRC", 0x3C),
            Word("ctrlram-destination-address-in-sram", "CtrlRAM destination address in SRAM", 0x40),
            Word("ctrlram-start-address-in-bin", "CtrlRAM start address in BIN", 0x48),
            Word("ctrlram-crc", "CtrlRAM CRC", 0x4C),
            Word("mp-ctrlram-destination-address-in-sram", "MP CtrlRAM destination address in SRAM", 0x50),
            Word("mp-ctrlram-start-address-in-bin", "MP CtrlRAM start address in BIN", 0x58),
            Word("mp-ctrlram-crc", "MP CtrlRAM CRC", 0x5C),
            Word("header-destination-address-in-sram", "Header destination address in SRAM", 0xF0),
            Word("header-size", "Header size", 0xF4),
            Word("header-start-address-in-bin", "Header start address in BIN", 0xF8),
            Word("header-crc", "Header CRC", 0xFC),
        ];
        return fields;
    }

    private static TpHeaderLayout CreateNt51927Layout()
    {
        List<TpHeaderField> fields =
        [
            Word("svn-auto-build-version", "SVN auto-build version", 0x24),
            Word("ov-info", "OV info", 0x28),
            Word("fw-config-destination-address-in-sram", "FW Config destination address in SRAM", 0x30),
            Word("fw-config-start-address-in-bin", "FW Config start address in BIN", 0x38),
            Word("fw-config-crc", "FW Config CRC", 0x3C),
            Word("ctrlram-destination-address-in-sram", "CtrlRAM destination address in SRAM", 0x40),
            Word("ctrlram-start-address-in-bin", "CtrlRAM start address in BIN", 0x48),
            Word("ctrlram-crc", "CtrlRAM CRC", 0x4C),
            Word("mp-ctrlram-destination-address-in-sram", "MP CtrlRAM destination address in SRAM", 0x50),
            Word("mp-ctrlram-start-address-in-bin", "MP CtrlRAM start address in BIN", 0x58),
            Word("mp-ctrlram-crc", "MP CtrlRAM CRC", 0x5C),
            Word("header-destination-address-in-sram", "Header destination address in SRAM", 0xF0),
            Word("header-size", "Header size", 0xF4),
            Word("header-start-address-in-bin", "Header start address in BIN", 0xF8),
            Word("header-crc", "Header CRC", 0xFC),
            Field("command-calibration", "Command header calibration", 0x200, 2),
            Field("command-build-read-command", "Command header build read command", 0x202, 1),
            Field("command-build-divider-count", "Command header build divider count", 0x203, 1),
            Field("command-build-t2-t1", "Command header build T2/T1", 0x204, 1),
            Field("command-build-t4-t3", "Command header build T4/T3", 0x205, 1),
            Field("command-build-t6-t5", "Command header build T6/T5", 0x206, 1),
            Field("command-build-t8-t7", "Command header build T8/T7", 0x207, 1),
            Field("command-build-t9", "Command header build T9", 0x208, 1),
            Word("common-header-crc", "Common Header CRC", 0x21C),
        ];
        // Worksheet 927 lists Header #0 through #2 followed by a continuation marker. The owner-approved
        // NT51927 three-chip final-header copy reads source [0x0000, 0x0460), which covers Header #3 at 0x02B0.
        // This extends report semantics only; it does not authorize any firmware operation.
        for (int index = 0; index <= 3; index++)
        {
            long start = 0x220 + (index * 0x30);
            fields.AddRange(
            [
                Word($"header-{index}-ilm-start-address-in-bin", $"ILM start address in BIN {index}", start),
                Word($"header-{index}-ilm-destination-address-in-sram", $"ILM destination address in SRAM {index}", start + 0x04),
                Word($"header-{index}-ilm-size", $"ILM size {index}", start + 0x08),
                Word($"header-{index}-ilm-crc", $"ILM CRC {index}", start + 0x0C),
                Word($"header-{index}-data-start-address-in-bin", $"DATA start address in BIN {index}", start + 0x10),
                Word($"header-{index}-data-destination-address-in-sram", $"DATA destination address in SRAM {index}", start + 0x14),
                Word($"header-{index}-data-size", $"DATA size {index}", start + 0x18),
                Word($"header-{index}-dlm-crc", $"DLM CRC {index}", start + 0x1C),
                Field($"header-{index}-ic-location", $"IC location {index}", start + 0x20, 1),
                Word($"header-{index}-next-header-address", $"Next header address {index}", start + 0x28),
                Word($"header-{index}-header-crc", $"Header CRC {index}", start + 0x2C),
            ]);
        }

        return new TpHeaderLayout(
            "nt51927-header-927",
            "NT51927 TP header",
            TpHeaderModelStatus.WorkbookWithPostbuildContinuation,
            [new ByteRange(0x0000, 0x0100), new ByteRange(0x0200, 0x00E0)],
            fields,
            "TDDI_Flash_Header.xlsx worksheet '927' (Header #0 through #2 and continuation marker); " +
            "PostbuildSetup_51927_1.4.1.bat three-chip final-header copy source [0x0000, 0x0460) confirms Header #3 coverage.");
    }

    private static TpHeaderLayout CreateNt51932TypeAbLayout()
    {
        List<TpHeaderField> fields =
        [
            Word("header-crc", "Header CRC", 0x7100),
            Word("ilm-destination-address-in-sram", "ILM destination address in SRAM", 0x7104),
            Word("ilm-size", "ILM size", 0x7108),
            Word("ilm-crc-0", "ILM CRC 0", 0x710C),
            Word("dlm-destination-address-in-sram", "DLM destination address in SRAM", 0x7110),
            Word("dlm-size", "DLM size", 0x7114),
            Word("dlm-crc-0", "DLM CRC 0", 0x7118),
            Word("dlm-diff-destination-address-in-sram", "DLM DIFF destination address in SRAM", 0x711C),
            Field("dlm-diff-size", "DLM DIFF size", 0x7120, 2),
            Field("build-read-command", "Build read command", 0x7124, 1),
            Field("build-divider-count", "Build divider count", 0x7125, 1),
            Field("spi-option", "SPI option", 0x7126, 1),
        ];
        for (int index = 1; index <= 7; index++)
        {
            fields.Add(Word($"dlm-crc-{index}", $"DLM CRC {index}", 0x7128 + ((index - 1) * 4)));
        }

        return new TpHeaderLayout(
            "nt51932-header-932-type-ab",
            "NT51932 TP header Type A/B fields",
            TpHeaderModelStatus.Workbook,
            [new ByteRange(0x7100, 0x0100)],
            fields,
            "TDDI_Flash_Header.xlsx worksheet '932' Type A (1-3 IC) and Type B (1-8 IC); " +
            "owner confirmation 2026-07-24 limits current NT51919/29/32 cascade use to 2-8 IC.");
    }

    private static TpHeaderLayout CreateNt51950Layout()
    {
        List<TpHeaderField> fields =
        [
            Word("ilm-start-address-in-bin", "ILM start address in BIN", 0xA100),
            Word("ilm-destination-address-in-sram", "ILM destination address in SRAM", 0xA104),
            Word("ilm-size", "ILM size", 0xA108),
            Word("ilm-crc-0", "ILM CRC 0", 0xA10C),
            Word("dlm-start-address-in-bin", "DLM start address in BIN", 0xA110),
            Word("dlm-destination-address-in-sram", "DLM destination address in SRAM", 0xA114),
            Word("dlm-size", "DLM size", 0xA118),
            Word("dlm-crc-0", "DLM CRC 0", 0xA11C),
            Field("build-read-command", "Build read command", 0xA12A, 1),
            Field("build-divider-count", "Build divider count", 0xA12B, 1),
            Field("spi-option", "SPI option", 0xA12C, 1),
            Word("header-crc", "Header CRC", 0xA130),
        ];
        for (int index = 1; index <= 19; index++)
        {
            fields.Add(Word($"dlm-crc-{index}", $"DLM CRC {index}", 0xA134 + ((index - 1) * 4)));
        }

        return new TpHeaderLayout(
            "nt51950-header-950",
            "NT51950 TP header",
            TpHeaderModelStatus.Workbook,
            [new ByteRange(0xA100, 0x0100)],
            fields,
            "TDDI_Flash_Header.xlsx worksheet '950'; owner confirmation 2026-07-24 classifies " +
            "Reserved (DIFF CRC) words [0xA134,0xA180) as DLM CRC 1 through 19.");
    }

    private static TpHeaderField Word(string fieldId, string displayName, long start)
    {
        return Field(fieldId, displayName, start, 4);
    }

    private static TpHeaderField Field(string fieldId, string displayName, long start, long length)
    {
        return new TpHeaderField(fieldId, displayName, new ByteRange(start, length));
    }
}
