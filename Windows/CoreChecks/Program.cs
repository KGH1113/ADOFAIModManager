using System.IO.Compression;
using System.Text;
using NativeUmm;

if (GameLayout.Detect(Path.Combine(Path.GetTempPath(), "adofai-does-not-exist")) is not null)
    throw new InvalidOperationException("Invalid game directory was accepted.");

var testRoot = Path.Combine(Path.GetTempPath(), $"adofai-windows-core-checks-{Guid.NewGuid():N}");
try
{
    var managed = Path.Combine(testRoot, "A Dance of Fire and Ice_Data", "Managed");
    Directory.CreateDirectory(managed);
    File.WriteAllBytes(Path.Combine(testRoot, "A Dance of Fire and Ice.exe"), []);
    File.WriteAllBytes(Path.Combine(managed, Spec.EntryPoint.AssemblyName), []);
    File.WriteAllBytes(Path.Combine(managed, "Assembly-CSharp.dll"), []);
    var layout = GameLayout.Detect(testRoot)
        ?? throw new InvalidOperationException("A valid test game directory was rejected.");

    var validZip = Path.Combine(testRoot, "valid.zip");
    using (var archive = ZipFile.Open(validZip, ZipArchiveMode.Create))
    {
        WriteEntry(archive, "ExampleMod/Info.json",
            "{\"Id\":\"ExampleMod\",\"DisplayName\":\"Example Mod\",\"Version\":\"1.0.0\"}");
        WriteEntry(archive, "ExampleMod/ExampleMod.dll", "test");
    }
    var inspection = ModManager.Inspect(layout, validZip);
    if (inspection.Id != "ExampleMod" || inspection.DisplayName != "Example Mod")
        throw new InvalidOperationException("A valid mod archive was not inspected correctly.");

    var traversalZip = Path.Combine(testRoot, "traversal.zip");
    using (var archive = ZipFile.Open(traversalZip, ZipArchiveMode.Create))
    {
        WriteEntry(archive, "../Info.json", "{\"Id\":\"Unsafe\"}");
    }
    ExpectInvalidArchive(layout, traversalZip, "path traversal");

    var ratioZip = Path.Combine(testRoot, "ratio.zip");
    using (var archive = ZipFile.Open(ratioZip, ZipArchiveMode.Create))
    {
        var entry = archive.CreateEntry("large-zero-file.bin", CompressionLevel.SmallestSize);
        using var stream = entry.Open();
        var zeros = new byte[1024 * 1024];
        for (var i = 0; i < 9; i++)
            stream.Write(zeros);
    }
    ExpectInvalidArchive(layout, ratioZip, "unsafe compression ratio");

    Console.WriteLine("Windows core checks passed: layout validation, valid mod inspection, path traversal, and ZIP ratio guards.");
}
finally
{
    if (Directory.Exists(testRoot))
        Directory.Delete(testRoot, recursive: true);
}

static void WriteEntry(ZipArchive archive, string name, string value)
{
    var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
    using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
    writer.Write(value);
}

static void ExpectInvalidArchive(GameLayout layout, string path, string scenario)
{
    try
    {
        _ = ModManager.Inspect(layout, path);
        throw new InvalidOperationException($"The {scenario} archive was accepted.");
    }
    catch (InvalidOperationException ex) when (!ex.Message.Contains("was accepted", StringComparison.Ordinal))
    {
    }
}
