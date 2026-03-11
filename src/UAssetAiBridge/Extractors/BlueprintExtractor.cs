using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;
using UAssetAiBridge.Models;

namespace UAssetAiBridge.Extractors;

class BlueprintExtractor : IAssetExtractor
{
    public string AssetTypeName => "Blueprint";

    public bool CanHandle(UAsset asset) =>
        asset.Imports.Any(i =>
            i.ObjectName.Value.Value == "Blueprint" &&
            i.ClassName.Value.Value == "Class");

    public SemanticAsset Extract(UAsset asset)
    {
        var name = Path.GetFileNameWithoutExtension(asset.FilePath);

        var parentClass = ResolveParentClass(asset);
        var variables   = ExtractVariables(asset, name);
        var functions   = ExtractFunctions(asset);

        return new SemanticAsset(
            AssetType: "Blueprint",
            Name: name,
            EngineVersion: "4.27",
            Content: new { parentClass, variables, functions }
        );
    }

    static string ResolveParentClass(UAsset asset)
    {
        var bpExport = asset.Exports
            .OfType<NormalExport>()
            .FirstOrDefault(e => e.ClassIndex.IsImport() &&
                asset.Imports[-e.ClassIndex.Index - 1].ObjectName.Value.Value == "Blueprint");
        if (bpExport == null) return "Unknown";

        var parentProp = bpExport.Data
            .OfType<ObjectPropertyData>()
            .FirstOrDefault(p => p.Name.Value.Value == "ParentClass");
        if (parentProp == null || !parentProp.IsImport()) return "Unknown";

        var imp = asset.Imports[-parentProp.Value.Index - 1];
        return imp.ObjectName.Value.Value;
    }

    static List<object> ExtractVariables(UAsset asset, string bpName)
    {
        // Variables with defaults live on the CDO export: Default__<BpName>_C
        var cdoName   = $"Default__{bpName}_C";
        var cdoExport = asset.Exports
            .OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.Value.Value == cdoName);

        if (cdoExport == null) return [];

        var vars = new List<object>();
        foreach (var prop in cdoExport.Data)
        {
            string propName = prop.Name.Value.Value;
            if (propName is "UberGraphFrame" or "None") continue;

            var (typeName, defaultVal) = ScalarValue(prop);
            vars.Add(new { name = propName, type = typeName, @default = defaultVal });
        }
        return vars;
    }

    static List<object> ExtractFunctions(UAsset asset)
    {
        var funcs = new List<object>();

        // Custom events declared in the graph
        foreach (var exp in asset.Exports.OfType<NormalExport>())
        {
            string cls = exp.ClassIndex.IsImport()
                ? asset.Imports[-exp.ClassIndex.Index - 1].ObjectName.Value.Value
                : "";
            if (cls != "K2Node_CustomEvent") continue;

            var nameProp = exp.Data
                .OfType<NamePropertyData>()
                .FirstOrDefault(p => p.Name.Value.Value == "CustomFunctionName");
            if (nameProp == null) continue;

            funcs.Add(new { name = nameProp.Value.Value.Value, kind = "event" });
        }

        // Non-system FunctionExports
        foreach (var exp in asset.Exports.OfType<FunctionExport>())
        {
            string funcName = exp.ObjectName.Value.Value;
            // Skip auto-generated UE internals
            if (funcName.StartsWith("ExecuteUbergraph_") ||
                funcName.StartsWith("BndEvt__") ||
                funcName is "GetModuleName" or "ReceiveBeginPlay") continue;

            if (funcs.Any(f => ((dynamic)f).name == funcName)) continue;

            funcs.Add(new { name = funcName, kind = "function" });
        }

        return funcs;
    }

    static (string type, object? val) ScalarValue(UAssetAPI.PropertyTypes.Objects.PropertyData prop) =>
        prop switch
        {
            FloatPropertyData  f => ("float",  (object?)f.Value),
            IntPropertyData    i => ("int",    i.Value),
            BoolPropertyData   b => ("bool",   b.Value),
            StrPropertyData    s => ("string", s.Value?.Value),
            NamePropertyData   n => ("name",   n.Value.Value.Value),
            EnumPropertyData   e => ("enum",   e.Value.Value.Value),
            ObjectPropertyData o => ("object", null),
            _                    => (prop.GetType().Name.Replace("PropertyData", ""), null)
        };
}
