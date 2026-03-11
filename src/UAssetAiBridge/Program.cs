using System.Text.Json;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAiBridge.Extractors;
using UAssetAiBridge.Writer;

static class Program
{
    const EngineVersion ENGINE = EngineVersion.VER_UE4_27;

    static readonly IAssetExtractor[] Extractors =
    [
        new WidgetBlueprintExtractor(),
        new BlueprintExtractor(),
        new FallbackExtractor(),
    ];

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static int Main(string[] args) => args switch
    {
        ["inspect",         var path]              => RunInspect(path),
        ["dump-json",       var path]              => RunDumpJson(path),
        ["modify-property", var path, var patch]   => RunModify(path, patch, ParseFlags(args)),
        ["probe",           var path]              => RunProbe(path),
        _ => Fail("usage",
            "Usage: uasset-ai-bridge <inspect|dump-json|modify-property> <file.uasset> [patch.json] [--output <out>] [--overwrite]", 2)
    };

    static int RunInspect(string path)
    {
        if (!File.Exists(path)) return Fail("file_not_found", $"File not found: {path}", 3);
        try
        {
            var asset     = new UAsset(path, ENGINE);
            var extractor = Extractors.First(e => e.CanHandle(asset));
            var assetType = extractor.AssetTypeName;

            Ok(new
            {
                assetType,
                name          = Path.GetFileNameWithoutExtension(path),
                engineVersion = "4.27",
                exportCount   = asset.Exports.Count,
                importCount   = asset.Imports.Count,
                sizeBytes     = new FileInfo(path).Length
            });
            return 0;
        }
        catch (Exception ex) { return Fail("parse_failed", ex.Message, 4); }
    }

    static int RunDumpJson(string path)
    {
        if (!File.Exists(path)) return Fail("file_not_found", $"File not found: {path}", 3);
        try
        {
            var asset     = new UAsset(path, ENGINE);
            var extractor = Extractors.First(e => e.CanHandle(asset));
            var result    = extractor.Extract(asset);

            Ok(new
            {
                assetType     = result.AssetType,
                name          = result.Name,
                engineVersion = result.EngineVersion,
                content       = result.Content
            });
            return 0;
        }
        catch (Exception ex) { return Fail("parse_failed", ex.Message, 4); }
    }

    static Dictionary<string, object?> ProbeProperty(UAssetAPI.PropertyTypes.Objects.PropertyData p)
    {
        var d = new Dictionary<string, object?>
        {
            ["name"] = p.Name.Value.Value,
            ["type"] = p.GetType().Name
        };
        if (p is UAssetAPI.PropertyTypes.Structs.StructPropertyData sp)
            d["children"] = sp.Value?.Select(ProbeProperty).ToList();
        else if (p is UAssetAPI.PropertyTypes.Objects.ArrayPropertyData ap)
            d["items"] = ap.Value?.Select(ProbeProperty).ToList();
        else
            d["val"] = p.RawValue?.ToString() ?? "";
        return d;
    }

    static Dictionary<string, string> ParseFlags(string[] args)
    {
        var flags = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--output" && i + 1 < args.Length) flags["output"] = args[++i];
            if (args[i] == "--overwrite") flags["overwrite"] = "true";
        }
        return flags;
    }

    static int RunModify(string assetPath, string patchPath, Dictionary<string, string> flags)
    {
        if (!File.Exists(assetPath)) return Fail("file_not_found", $"File not found: {assetPath}", 3);
        if (!File.Exists(patchPath)) return Fail("file_not_found", $"Patch file not found: {patchPath}", 3);

        try
        {
            var patch = Patch.FromJson(File.ReadAllText(patchPath));
            var asset = new UAsset(assetPath, ENGINE);

            AssetPatcher.Apply(asset, patch);

            bool overwrite = flags.ContainsKey("overwrite");
            string outputPath = flags.TryGetValue("output", out var o) ? o
                : overwrite ? assetPath
                : Path.Combine(
                    Path.GetDirectoryName(assetPath) ?? ".",
                    Path.GetFileNameWithoutExtension(assetPath) + "_patched.uasset");

            if (outputPath == assetPath && !overwrite)
                return Fail("safety", "Output path equals source. Use --overwrite to allow in-place modification.", 2);

            asset.Write(outputPath);

            // Post-write validation: reload and confirm it parses
            try { _ = new UAsset(outputPath, ENGINE); }
            catch (Exception ex)
            {
                File.Delete(outputPath);
                return Fail("validation_failed", $"Written asset failed to reload: {ex.Message}", 6);
            }

            Ok(new { status = "ok", output = outputPath });
            return 0;
        }
        catch (NotSupportedException ex) { return Fail("patch_invalid", ex.Message, 5); }
        catch (ArgumentException      ex) { return Fail("patch_invalid", ex.Message, 5); }
        catch (InvalidOperationException ex) { return Fail("patch_invalid", ex.Message, 5); }
        catch (Exception ex) { return Fail("parse_failed", ex.Message, 4); }
    }

    static int RunProbe(string path)
    {
        if (!File.Exists(path)) return Fail("file_not_found", $"File not found: {path}", 3);
        var asset = new UAsset(path, ENGINE);
        var result = asset.Exports
            .OfType<UAssetAPI.ExportTypes.NormalExport>()
            .Select(e => new
            {
                name  = e.ObjectName.Value.Value,
                outer = e.OuterIndex.Index,
                cls   = e.ClassIndex.IsImport()
                    ? asset.Imports[-e.ClassIndex.Index - 1].ObjectName.Value.Value
                    : "?",
                props = e.Data.Select(p => ProbeProperty(p))
            });
        Ok(result);
        return 0;
    }

    static void Ok(object data) =>
        Console.WriteLine(JsonSerializer.Serialize(data, JsonOpts));

    static int Fail(string code, string message, int exitCode)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { error = code, message }, JsonOpts));
        return exitCode;
    }
}
