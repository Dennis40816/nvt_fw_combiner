namespace NvtFwCombiner.UiSmoke.Tests;

internal static class FirmwareByteTestData
{
    internal static byte[] CreatePattern(int length, byte seed)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(seed + (index % 251)));
        }

        return bytes;
    }

    internal static byte[] CreateHexPattern(int length)
    {
        return [.. Enumerable.Range(0, length).Select(index => (byte)(index % 251))];
    }

    internal static void WriteUiAbCmi(
        byte[] image,
        int bankStart,
        byte major,
        byte minor,
        ushort jira)
    {
        const int register16Offset = 0x401A;
        int start = checked(bankStart + register16Offset);
        image[start] = checked((byte)(jira & 0xFF));
        image[start + 1] = major;
        image[start + 2] = checked((byte)((minor << 4) | ((jira >> 8) & 0x0F)));
    }
}
