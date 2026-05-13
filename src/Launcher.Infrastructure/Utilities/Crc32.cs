namespace Launcher.Infrastructure.Utilities;

public static class Crc32
{
    private static readonly uint[] Table = CreateTable();

    public static uint Compute(Stream stream)
    {
        uint crc = 0xFFFFFFFF;
        var buffer = new byte[8192];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                crc = (crc >> 8) ^ Table[(crc ^ buffer[i]) & 0xFF];
            }
        }
        return ~crc;
    }

    private static uint[] CreateTable()
    {
        const uint poly = 0xEDB88320;
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            uint value = i;
            for (var j = 0; j < 8; j++)
            {
                value = (value & 1) == 1 ? (value >> 1) ^ poly : value >> 1;
            }
            table[i] = value;
        }
        return table;
    }
}
