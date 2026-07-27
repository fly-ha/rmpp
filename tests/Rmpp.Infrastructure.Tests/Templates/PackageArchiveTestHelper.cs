using System.IO.Compression;
using System.Text;
using Rmpp.Infrastructure.Templates;

namespace Rmpp.Infrastructure.Tests.Templates;

internal static class PackageArchiveTestHelper
{
    public static async Task<MemoryStream> CreatePackageAsync()
    {
        MemoryStream stream = new();
        await new RmppPackageWriter().WriteAsync(stream, TemplatePackageTestData.Create());
        stream.Position = 0;
        return stream;
    }

    public static void ReplaceTextEntry(MemoryStream stream, string path, Func<string, string> transform)
    {
        stream.Position = 0;
        using ZipArchive archive = new(stream, ZipArchiveMode.Update, leaveOpen: true);
        ZipArchiveEntry entry = archive.GetEntry(path)
            ?? throw new InvalidOperationException($"Missing test package entry: {path}");
        string original;
        using (Stream input = entry.Open())
        using (StreamReader reader = new(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            original = reader.ReadToEnd();
        }

        entry.Delete();
        WriteTextEntry(archive, path, transform(original));
    }

    public static string ReadTextEntry(MemoryStream stream, string path)
    {
        stream.Position = 0;
        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: true);
        ZipArchiveEntry entry = archive.GetEntry(path)
            ?? throw new InvalidOperationException($"Missing test package entry: {path}");
        using Stream input = entry.Open();
        using StreamReader reader = new(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static void WriteTextEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path);
        using Stream output = entry.Open();
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        output.Write(bytes);
    }
}
