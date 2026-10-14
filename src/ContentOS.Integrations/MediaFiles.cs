using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Integrations;

public static class MediaFiles
{
    public static uint Crc(ReadOnlySpan<byte> data) { uint crc = uint.MaxValue; foreach (var b in data) { crc ^= b; for (var n = 0; n < 8; n++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1; } return ~crc; }
    public static MediaInfo Inspect(byte[] bytes)
    {
        if (bytes.Length is < 1 or > 1048576) throw new RuntimeValidationException("Media size exceeds 1MiB.");
        if (bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) { try { return Png(bytes); } catch (Exception e) when (e is InvalidDataException or EndOfStreamException) { throw new RuntimeValidationException("Invalid PNG compressed data."); } }
        if (bytes.Length >= 44 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WAVE") return Wav(bytes);
        throw new RuntimeValidationException("M16 accepts bounded noninterlaced RGB/RGBA8 PNG and PCM16 WAV only; other formats need a qualified processor.");
    }
    private static MediaInfo Png(byte[] b)
    {
        int p = 8, w = 0, h = 0, channels = 0, count = 0; bool ended = false; using var compressed = new MemoryStream();
        while (p + 12 <= b.Length)
        {
            if (++count > 100) throw new RuntimeValidationException("PNG chunk limit."); var n = (int)BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(p, 4)); if (n < 0 || n > b.Length - p - 12) throw new RuntimeValidationException("Invalid PNG chunk length."); var name = Encoding.ASCII.GetString(b, p + 4, 4); if (Crc(b.AsSpan(p + 4, n + 4)) != BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(p + 8 + n, 4))) throw new RuntimeValidationException("PNG CRC mismatch.");
            if (name == "IHDR") { if (p != 8 || n != 13) throw new RuntimeValidationException("PNG header order."); w = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(p + 8, 4)); h = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(p + 12, 4)); var color = b[p + 17]; if (w is < 1 or > 1024 || h is < 1 or > 1024 || (long)w * h > 1048576 || b[p + 16] != 8 || color is not (2 or 6) || b[p + 18] != 0 || b[p + 19] != 0 || b[p + 20] != 0) throw new RuntimeValidationException("Unsupported bounded PNG dimensions/encoding."); channels = color == 2 ? 3 : 4; }
            else if (name == "IDAT") { if (w == 0 || ended) throw new RuntimeValidationException("PNG data order."); compressed.Write(b, p + 8, n); }
            else if (name == "IEND") { if (n != 0 || compressed.Length == 0) throw new RuntimeValidationException("Invalid PNG end."); ended = true; p += 12; break; }
            else throw new RuntimeValidationException("PNG ancillary/active metadata unsupported; re-export a plain image.");
            p += n + 12;
        }
        if (!ended || p != b.Length) throw new RuntimeValidationException("Incomplete PNG.");
        compressed.Position = 0; using var z = new ZLibStream(compressed, CompressionMode.Decompress); var row = new byte[1 + w * channels]; for (var y = 0; y < h; y++) { z.ReadExactly(row); if (row[0] > 4) throw new RuntimeValidationException("Invalid PNG filter."); }
        if (z.ReadByte() != -1) throw new RuntimeValidationException("PNG expanded-size mismatch.");
        return new("image/png", w, h, null, MediaRules.Hash(b), b.Length);
    }
    private static MediaInfo Wav(byte[] b)
    {
        if (BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(4, 4)) != b.Length - 8) throw new RuntimeValidationException("WAV container length mismatch.");
        int p = 12, rate = 0, align = 0, data = 0; bool fmt = false;
        while (p + 8 <= b.Length)
        {
            var name = Encoding.ASCII.GetString(b, p, 4); var n = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p + 4, 4)); if (n < 0 || n > b.Length - p - 8) throw new RuntimeValidationException("WAV chunk length.");
            if (name == "fmt ") { if (fmt || n is not (16 or 18)) throw new RuntimeValidationException("WAV PCM format required."); fmt = true; var f = b.AsSpan(p + 8, n); var channels = BinaryPrimitives.ReadUInt16LittleEndian(f[2..]); rate = BinaryPrimitives.ReadInt32LittleEndian(f[4..]); align = BinaryPrimitives.ReadUInt16LittleEndian(f[12..]); if (BinaryPrimitives.ReadUInt16LittleEndian(f) != 1 || channels is < 1 or > 2 || BinaryPrimitives.ReadUInt16LittleEndian(f[14..]) != 16 || rate is < 8000 or > 48000 || align != channels * 2 || BinaryPrimitives.ReadInt32LittleEndian(f[8..]) != rate * align) throw new RuntimeValidationException("Bounded PCM16 WAV required."); }
            else if (name == "data") { if (!fmt || data != 0 || n == 0 || n % align != 0) throw new RuntimeValidationException("Invalid WAV data."); data = n; }
            else throw new RuntimeValidationException("WAV metadata unsupported.");
            p += 8 + n + (n % 2);
        }
        if (p != b.Length || data == 0 || (double)data / (rate * align) > 30) throw new RuntimeValidationException("Complete WAV up to30 seconds required.");
        return new("audio/wav", null, null, (double)data / (rate * align), MediaRules.Hash(b), b.Length);
    }
    public static byte[] ReferenceCard(string identity, string[] palette)
    {
        const int w = 256, h = 192; var rgb = new byte[h * (1 + w * 3)]; var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        byte[] color = palette.Length > 0 ? Convert.FromHexString(palette[0].TrimStart('#')) : hash[..3];
        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) { var pos = y * (1 + w * 3) + 1 + x * 3; var face = (x - 64) * (x - 64) + (y - 55) * (y - 55) < 625 || Math.Abs(x - 160) < 20 && Math.Abs(y - 55) < 25; var body = (Math.Abs(x - 64) < 23 || Math.Abs(x - 160) < 18) && y > 80 && y < 145; var dark = (y is > 147 and < 151) || (x is > 20 and < 23); rgb[pos] = face ? (byte)235 : body ? color[0] : dark ? (byte)35 : (byte)248; rgb[pos + 1] = face ? (byte)199 : body ? color[1] : dark ? (byte)50 : (byte)248; rgb[pos + 2] = face ? (byte)152 : body ? color[2] : dark ? (byte)45 : (byte)245; }
        using var encoded = new MemoryStream(); using (var z = new ZLibStream(encoded, CompressionLevel.Optimal, true)) z.Write(rgb); using var result = new MemoryStream(); result.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        void Chunk(string type, byte[] content) { var name = Encoding.ASCII.GetBytes(type); var len = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(len, content.Length); result.Write(len); result.Write(name); result.Write(content); var all = name.Concat(content).ToArray(); BinaryPrimitives.WriteUInt32BigEndian(len, Crc(all)); result.Write(len); }
        var header = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header, w); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), h); header[8] = 8; header[9] = 2; Chunk("IHDR", header); Chunk("IDAT", encoded.ToArray()); Chunk("IEND", []); return result.ToArray();
    }
}
public sealed class MediaFileInspector : IMediaFileInspector { public MediaInfo Inspect(byte[] bytes) => MediaFiles.Inspect(bytes); }
