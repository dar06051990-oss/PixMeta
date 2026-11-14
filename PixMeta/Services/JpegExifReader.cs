using System.Buffers.Binary;
using System.Text;

namespace PixMeta.Services;

internal static class JpegExifReader
{
    public static Dictionary<string,string> Read(string path)
    {
        var result = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var b = File.ReadAllBytes(path);
        if (b.Length >= 12 && Encoding.ASCII.GetString(b, 0, 4) == "RIFF"
            && Encoding.ASCII.GetString(b, 8, 4) == "WEBP")
        {
            // WEBP stores EXIF in a RIFF EXIF chunk, not a JPEG APP1 segment.
            int at = 12;
            while (at + 8 <= b.Length)
            {
                uint size = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at + 4, 4));
                long next = (long)at + 8 + size + (size & 1);
                if (next > b.Length) break;
                if (Encoding.ASCII.GetString(b, at, 4) == "EXIF")
                {
                    int offset = at + 8;
                    int count = (int)size;
                    if (count >= 6 && Encoding.ASCII.GetString(b, offset, 6) == "Exif\0\0")
                    { offset += 6; count -= 6; }
                    ParseTiff(b.AsSpan(offset, count), result);
                    break;
                }
                at = (int)next;
            }
            return result;
        }
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) return result;
        int p=2;
        while(p+4<=b.Length && b[p]==0xFF)
        {
            byte marker=b[p+1]; p+=2;
            if(marker==0xD9 || marker==0xDA) break;
            if(p+2>b.Length) break;
            int len=BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(p,2));
            if(len<2 || p+len>b.Length) break;
            if(marker==0xE1 && len>=8 && Encoding.ASCII.GetString(b,p+2,6)=="Exif\0\0")
            { ParseTiff(b.AsSpan(p+8,len-8),result); break; }
            p+=len;
        }
        return result;
    }

    static void ParseTiff(ReadOnlySpan<byte> tiffSpan, Dictionary<string,string> r)
    {
        // Copy the TIFF payload to a normal byte[] so parsing helpers don't capture
        // a ref-like ReadOnlySpan inside local functions (CS9108).
        var t = tiffSpan.ToArray();
        if (t.Length < 8) return;
        bool le = t[0] == (byte)'I' && t[1] == (byte)'I';
        if (!le && !(t[0] == (byte)'M' && t[1] == (byte)'M')) return;

        ushort U16(int o)
        {
            if (o < 0 || o + 2 > t.Length) return 0;
            return le
                ? BinaryPrimitives.ReadUInt16LittleEndian(t.AsSpan(o, 2))
                : BinaryPrimitives.ReadUInt16BigEndian(t.AsSpan(o, 2));
        }

        uint U32(int o)
        {
            if (o < 0 || o + 4 > t.Length) return 0;
            return le
                ? BinaryPrimitives.ReadUInt32LittleEndian(t.AsSpan(o, 4))
                : BinaryPrimitives.ReadUInt32BigEndian(t.AsSpan(o, 4));
        }

        uint exifIfd = 0;
        ParseIfd((int)U32(4), ref exifIfd);
        if (exifIfd > 0) ParseIfd((int)exifIfd, ref exifIfd);

        void ParseIfd(int off, ref uint exifPtr)
        {
            if (off < 0 || off + 2 > t.Length) return;
            int n = U16(off), pos = off + 2;
            for (int i = 0; i < n && pos + 12 <= t.Length; i++, pos += 12)
            {
                ushort tag = U16(pos), type = U16(pos + 2);
                uint count = U32(pos + 4), vo = U32(pos + 8);
                int unit = type switch { 1 or 2 or 7 => 1, 3 => 2, 4 or 9 => 4, 5 or 10 => 8, _ => 1 };
                long bytes = (long)count * unit;
                if (bytes < 0 || bytes > int.MaxValue) continue;
                int dataOff = bytes <= 4 ? pos + 8 : (int)vo;
                if (dataOff < 0 || dataOff + bytes > t.Length) continue;
                var data = t.AsSpan(dataOff, (int)bytes);
                if (tag == 0x8769) { exifPtr = vo; continue; }
                if (tag == 0x010E) r["EXIF ImageDescription"] = Ascii(data);
                else if (tag == 0x010F) r["EXIF Make"] = Ascii(data);
                else if (tag == 0x0110) r["EXIF Model"] = Ascii(data);
                else if (tag == 0x0131) r["EXIF Software"] = Ascii(data);
                else if (tag == 0x013B) r["EXIF Artist"] = Ascii(data);
                else if (tag == 0x9286) r["EXIF UserComment"] = DecodeUserComment(data);
            }
        }
    }

    static string Ascii(ReadOnlySpan<byte> d)=>Encoding.UTF8.GetString(d).TrimEnd('\0',' ','\r','\n');
    static string DecodeUserComment(ReadOnlySpan<byte> d)
    {
        if(d.Length==0)return "";
        if(d.Length>=8)
        {
            var head=Encoding.ASCII.GetString(d[..8]); var body=d[8..];
            if(head.StartsWith("ASCII")) return Encoding.UTF8.GetString(body).TrimEnd('\0');
            if(head.StartsWith("UNICODE"))
            {
                while(body.Length>=2 && body[0]==0 && body[1]==0) body=body[2..];
                if(body.Length>=2 && body[0]==0xFE&&body[1]==0xFF) return Encoding.BigEndianUnicode.GetString(body[2..]).TrimEnd('\0');
                if(body.Length>=2 && body[0]==0xFF&&body[1]==0xFE) return Encoding.Unicode.GetString(body[2..]).TrimEnd('\0');
                // Preview Plus v0.1 uses UNICODE\0 as a marker but writes UTF-8 JSON.
                // Decode UTF-8 unless the byte pattern clearly looks like UTF-16.
                int evenZero=0,oddZero=0,pairs=Math.Min(body.Length/2,64);
                for(int i=0;i<pairs;i++){if(body[i*2]==0)evenZero++;if(body[i*2+1]==0)oddZero++;}
                if (pairs > 0 && Math.Max(evenZero, oddZero) > pairs / 3)
                    return (evenZero > oddZero ? Encoding.BigEndianUnicode : Encoding.Unicode)
                        .GetString(body[..(body.Length-body.Length%2)]).TrimEnd('\0');
                return Encoding.UTF8.GetString(body).TrimEnd('\0');
            }
        }
        return Encoding.UTF8.GetString(d).TrimEnd('\0');
    }
}
