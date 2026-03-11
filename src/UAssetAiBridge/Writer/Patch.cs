using System.Text.Json;

namespace UAssetAiBridge.Writer;

record Patch(string Operation, string Path, JsonElement? Value)
{
    public static Patch FromJson(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string op   = root.GetProperty("operation").GetString()
            ?? throw new ArgumentException("Patch missing 'operation'");
        string path = root.GetProperty("path").GetString()
            ?? throw new ArgumentException("Patch missing 'path'");
        JsonElement? value = root.TryGetProperty("value", out var v) ? v : null;

        return new Patch(op, path, value);
    }
}
