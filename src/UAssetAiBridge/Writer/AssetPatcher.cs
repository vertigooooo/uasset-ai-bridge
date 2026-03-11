using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;

namespace UAssetAiBridge.Writer;

static class AssetPatcher
{
    public static void Apply(UAsset asset, Patch patch)
    {
        if (patch.Operation != "set")
            throw new NotSupportedException($"Operation '{patch.Operation}' not supported in v1. Only 'set' is supported.");

        var segments = patch.Path.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2 || segments[0] != "content")
            throw new ArgumentException("Path must start with 'content'");

        var widgetTree = asset.Exports
            .OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.Value.Value == "WidgetTree")
            ?? throw new InvalidOperationException(
                "No WidgetTree found. Only WidgetBlueprint assets support modify-property.");

        var rootProp = widgetTree.Data
            .OfType<ObjectPropertyData>()
            .FirstOrDefault(p => p.Name.Value.Value == "RootWidget")
            ?? throw new InvalidOperationException("WidgetTree has no RootWidget.");

        if (!rootProp.IsExport())
            throw new InvalidOperationException("RootWidget is not an export reference.");

        var current = asset.Exports[rootProp.Value.Index - 1] as NormalExport
            ?? throw new InvalidOperationException("RootWidget is not a NormalExport.");

        // Walk path: skip "content", optionally skip "root", then navigate children
        int i = 1; // already consumed "content"
        if (i < segments.Length && segments[i] == "root") i++;

        while (i < segments.Length)
        {
            string seg = segments[i];

            if (seg.StartsWith("children["))
            {
                current = ResolveChild(asset, current, seg)
                    ?? throw new ArgumentException($"Path segment '{seg}' did not match any child widget.");
                i++;
            }
            else if (seg == "properties" && i + 1 < segments.Length)
            {
                SetProperty(asset, current, segments[i + 1], patch.Value);
                return;
            }
            else
            {
                throw new ArgumentException($"Unexpected path segment: '{seg}'");
            }
        }

        throw new ArgumentException("Path must end with 'properties.<name>'");
    }

    // ── Path navigation ──────────────────────────────────────────────────────

    static NormalExport? ResolveChild(UAsset asset, NormalExport parent, string segment)
    {
        // Segment format: "children[name=X]" or "children[0]"
        int bracketStart = segment.IndexOf('[') + 1;
        int bracketEnd   = segment.IndexOf(']');
        string selector  = segment[bracketStart..bracketEnd];

        int parentIdx1 = asset.Exports.IndexOf(parent) + 1;

        // Collect ordered slots owned by parent
        var slots = asset.Exports
            .Select((e, idx) => (export: e, idx1: idx + 1))
            .Where(x => x.export.OuterIndex.Index == parentIdx1
                     && x.export is NormalExport
                     && IsSlotClass(asset, x.export.ClassIndex))
            .Select(x => (slot: (NormalExport)x.export, x.idx1))
            .ToList();

        NormalExport? targetSlot = null;

        if (selector.StartsWith("name="))
        {
            string targetName = selector["name=".Length..];
            targetSlot = slots
                .Select(s => s.slot)
                .FirstOrDefault(slot => GetSlotContentName(asset, slot) == targetName);
        }
        else if (int.TryParse(selector, out int idx) && idx < slots.Count)
        {
            targetSlot = slots[idx].slot;
        }

        if (targetSlot == null) return null;

        var contentProp = targetSlot.Data
            .OfType<ObjectPropertyData>()
            .FirstOrDefault(p => p.Name.Value.Value == "Content");

        if (contentProp == null || !contentProp.IsExport()) return null;
        return asset.Exports[contentProp.Value.Index - 1] as NormalExport;
    }

    static bool IsSlotClass(UAsset asset, FPackageIndex classIndex) =>
        classIndex.IsImport() &&
        asset.Imports[-classIndex.Index - 1].ObjectName.Value.Value.EndsWith("Slot");

    static string? GetSlotContentName(UAsset asset, NormalExport slot)
    {
        var contentProp = slot.Data
            .OfType<ObjectPropertyData>()
            .FirstOrDefault(p => p.Name.Value.Value == "Content");

        if (contentProp == null || !contentProp.IsExport()) return null;
        return asset.Exports[contentProp.Value.Index - 1]?.ObjectName.Value.Value;
    }

    // ── Property mutation ────────────────────────────────────────────────────

    static void SetProperty(UAsset asset, NormalExport export, string propName, System.Text.Json.JsonElement? value)
    {
        switch (propName)
        {
            case "text":
                SetText(export, value?.GetString() ?? "");
                break;
            case "visibility":
                SetVisibility(asset, export, value?.GetString() ?? "Visible");
                break;
            default:
                throw new NotSupportedException(
                    $"Property '{propName}' is not supported for modification in v1. Supported: text, visibility.");
        }
    }

    static void SetText(NormalExport export, string newText)
    {
        foreach (var prop in export.Data)
        {
            if (prop.Name.Value.Value != "Text") continue;

            if (prop is TextPropertyData t)
            {
                t.Value               = new FString(newText);
                t.HistoryType         = TextHistoryType.RawText;
                t.CultureInvariantString = null;
                return;
            }
            if (prop is StrPropertyData s)
            {
                s.Value = new FString(newText);
                return;
            }
        }
        throw new InvalidOperationException("Widget has no 'Text' property. Only text-bearing widgets (TextBlock, etc.) support this.");
    }

    static void SetVisibility(UAsset asset, NormalExport export, string visibility)
    {
        // Accept both "Visible" and "ESlateVisibility::Visible"
        if (visibility.Contains("::"))
            visibility = visibility[(visibility.LastIndexOf("::") + 2)..];

        string[] valid = ["Visible", "Collapsed", "Hidden", "HitTestInvisible", "SelfHitTestInvisible"];
        if (!valid.Contains(visibility))
            throw new ArgumentException(
                $"Invalid visibility value '{visibility}'. Valid: {string.Join(", ", valid)}");

        string enumValue = "ESlateVisibility::" + visibility;

        foreach (var prop in export.Data)
        {
            if (prop.Name.Value.Value != "Visibility") continue;
            if (prop is not EnumPropertyData e) continue;

            asset.AddNameReference(new FString(enumValue));
            e.Value = new FName(asset, enumValue);
            return;
        }
        throw new InvalidOperationException("Widget has no 'Visibility' property.");
    }
}
