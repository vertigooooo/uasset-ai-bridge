using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAiBridge.Models;

namespace UAssetAiBridge.Extractors;

class WidgetBlueprintExtractor : IAssetExtractor
{
    public string AssetTypeName => "WidgetBlueprint";

    public bool CanHandle(UAsset asset) =>
        asset.Imports.Any(i =>
            i.ObjectName.Value.Value == "WidgetBlueprint" &&
            i.ClassName.Value.Value == "Class");

    public SemanticAsset Extract(UAsset asset)
    {
        var name = Path.GetFileNameWithoutExtension(asset.FilePath);

        var widgetTreeExport = asset.Exports
            .OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.Value.Value == "WidgetTree")
            ?? throw new InvalidOperationException("WidgetTree not found");

        var rootProp = widgetTreeExport.Data
            .OfType<ObjectPropertyData>()
            .FirstOrDefault(p => p.Name.Value.Value == "RootWidget");

        WidgetNode? root = null;
        if (rootProp is { } rp && rp.IsExport())
            root = ExtractWidget(asset, rp.Value.Index - 1);

        return new SemanticAsset(
            AssetType: "WidgetBlueprint",
            Name: name,
            EngineVersion: "4.27",
            Content: new { root }
        );
    }

    WidgetNode? ExtractWidget(UAsset asset, int idx0)
    {
        if (idx0 < 0 || idx0 >= asset.Exports.Count) return null;
        if (asset.Exports[idx0] is not NormalExport ne) return null;

        int idx1 = idx0 + 1;
        string type = ResolveClass(asset, ne.ClassIndex);
        string name = ne.ObjectName.Value.Value;
        var properties = ExtractWidgetProperties(ne);
        var children = ExtractChildren(asset, idx1, properties);

        return new WidgetNode(type, name, properties, children);
    }

    // properties dict is passed in so slot layout can be merged into it
    List<WidgetNode> ExtractChildren(UAsset asset, int parentIdx1, Dictionary<string, object?> parentProps)
    {
        var children = new List<WidgetNode>();

        // Panel widgets: children live in slot exports (outer == parent, class ends with "Slot")
        for (int i = 0; i < asset.Exports.Count; i++)
        {
            var exp = asset.Exports[i];
            if (exp.OuterIndex.Index != parentIdx1) continue;
            if (exp is not NormalExport slot) continue;
            if (!ResolveClass(asset, exp.ClassIndex).EndsWith("Slot")) continue;

            var contentProp = slot.Data
                .OfType<ObjectPropertyData>()
                .FirstOrDefault(p => p.Name.Value.Value == "Content");

            if (contentProp is not { } cp || !cp.IsExport()) continue;

            var child = ExtractWidget(asset, cp.Value.Index - 1);
            if (child == null) continue;

            // Merge CanvasPanelSlot layout into child widget properties
            MergeSlotLayout(slot, child.Properties);

            children.Add(child);
        }

        // Single-child widgets (Button etc.): direct Content property
        if (children.Count == 0 && asset.Exports[parentIdx1 - 1] is NormalExport parent)
        {
            var contentProp = parent.Data
                .OfType<ObjectPropertyData>()
                .FirstOrDefault(p => p.Name.Value.Value == "Content");

            if (contentProp is { } cp && cp.IsExport())
            {
                var child = ExtractWidget(asset, cp.Value.Index - 1);
                if (child != null) children.Add(child);
            }
        }

        return children;
    }

    static void MergeSlotLayout(NormalExport slot, Dictionary<string, object?> props)
    {
        var layoutData = slot.Data
            .OfType<StructPropertyData>()
            .FirstOrDefault(p => p.Name.Value.Value == "LayoutData");
        if (layoutData?.Value == null) return;

        var offsets = layoutData.Value
            .OfType<StructPropertyData>()
            .FirstOrDefault(p => p.Name.Value.Value == "Offsets");
        if (offsets?.Value == null) return;

        float left = 0, top = 0, right = 0, bottom = 0;
        foreach (var f in offsets.Value.OfType<FloatPropertyData>())
        {
            switch (f.Name.Value.Value)
            {
                case "Left":   left   = f.Value; break;
                case "Top":    top    = f.Value; break;
                case "Right":  right  = f.Value; break;
                case "Bottom": bottom = f.Value; break;
            }
        }

        // When anchors min==max: Left/Top = position, Right/Bottom = size
        props["position"] = new[] { left, top };
        props["size"]     = new[] { right, bottom };
    }

    static Dictionary<string, object?> ExtractWidgetProperties(NormalExport export)
    {
        var props = new Dictionary<string, object?>();

        foreach (var prop in export.Data)
        {
            string key = prop.Name.Value.Value;
            switch (prop)
            {
                case StrPropertyData s when key == "Text":
                    props["text"] = s.Value?.Value;
                    break;

                case TextPropertyData t when key == "Text":
                    var text = t.HistoryType switch
                    {
                        TextHistoryType.None    => t.CultureInvariantString?.Value,
                        TextHistoryType.Base    => t.Value?.Value,
                        TextHistoryType.RawText => t.Value?.Value,
                        _ => null
                    };
                    if (text != null) props["text"] = text;
                    break;

                case EnumPropertyData e when key == "Visibility":
                    // Strip "ESlateVisibility::" prefix if present
                    var vis = e.Value.Value.Value;
                    props["visibility"] = vis.Contains("::")
                        ? vis[(vis.LastIndexOf("::") + 2)..]
                        : vis;
                    break;
            }
        }

        return props;
    }

    static string ResolveClass(UAsset asset, FPackageIndex classIndex)
    {
        if (classIndex.IsImport())
            return asset.Imports[-classIndex.Index - 1].ObjectName.Value.Value;
        if (classIndex.IsExport())
            return asset.Exports[classIndex.Index - 1].ObjectName.Value.Value;
        return "Unknown";
    }
}
