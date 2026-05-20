using System.IO;
using System.IO.Compression;
using System.Text;

public static class GZipCompression
{
    public static byte[] Compress(string data)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(data);

        using (var outputStream = new MemoryStream())
        {
            using (var gZipStream = new GZipStream(outputStream, CompressionMode.Compress))
            {
                gZipStream.Write(inputBytes, 0, inputBytes.Length);
            }
            return outputStream.ToArray();
        }
    }

    public static string Decompress(byte[] compressedData)
    {
        using MemoryStream input = new MemoryStream(compressedData);
        using GZipStream gzip = new GZipStream(input, CompressionMode.Decompress);
        using StreamReader reader = new StreamReader(gzip);
        return reader.ReadToEnd();
    }

}
