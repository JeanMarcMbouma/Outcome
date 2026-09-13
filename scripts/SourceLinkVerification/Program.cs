using System.IO.Compression;
using System.Reflection.Metadata;
using System.Text.Json;

var sourceLinkKind = new Guid("CC110556-A091-4D38-9FEC-25AB9A351A6A");
var embeddedSourceKind = new Guid("0E8A571B-6926-466E-B4AD-8AB04611F5FE");
var directory = args.Length > 0 ? args[0] : "artifacts/packages";
var expected = new HashSet<string> { "BbQ.Outcome", "BbQ.Outcome.SourceGenerators", "BbQ.Cqrs.SourceGenerators", "BbQ.Events.SourceGenerators", "BbQ.Cqrs" };
var checkedSymbols = 0;
var embeddedSources = 0;
var netFrameworkChecked = false;
foreach (var path in Directory.EnumerateFiles(directory, "*.snupkg"))
{
    using var archive = ZipFile.OpenRead(path);
    foreach (var entry in archive.Entries.Where(e => e.FullName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)))
    {
        using var data = new MemoryStream();
        using (var input = entry.Open()) input.CopyTo(data);
        data.Position = 0;
        using var provider = MetadataReaderProvider.FromPortablePdbStream(data);
        var reader = provider.GetMetadataReader();
        var hasSourceLink = false;
        foreach (var handle in reader.CustomDebugInformation)
        {
            var information = reader.GetCustomDebugInformation(handle);
            var kind = reader.GetGuid(information.Kind);
            if (kind == embeddedSourceKind) embeddedSources++;
            if (kind != sourceLinkKind) continue;
            using var json = JsonDocument.Parse(reader.GetBlobBytes(information.Value));
            var documents = json.RootElement.GetProperty("documents").EnumerateObject().ToArray();
            if (documents.Length == 0 || documents.Any(d => !d.Value.GetString()!.StartsWith("https://raw.githubusercontent.com/JeanMarcMbouma/Outcome/", StringComparison.Ordinal)))
                throw new InvalidDataException($"Invalid Source Link in {path}:{entry.FullName}");
            hasSourceLink = true;
        }
        if (!hasSourceLink) throw new InvalidDataException($"Missing Source Link in {path}:{entry.FullName}");
        expected.Remove(Path.GetFileNameWithoutExtension(entry.Name));
        if (entry.FullName.Contains("net481/") && entry.Name == "BbQ.Cqrs.pdb") netFrameworkChecked = true;
        checkedSymbols++;
    }
}
if (expected.Count != 0 || !netFrameworkChecked)
    throw new InvalidDataException($"Missing expected symbols: {string.Join(", ", expected)}; net481 checked: {netFrameworkChecked}");
Console.WriteLine($"SOURCE_LINK_PASS: {checkedSymbols} portable PDBs, including all former package pins and CQRS net481; {embeddedSources} embedded source records.");
