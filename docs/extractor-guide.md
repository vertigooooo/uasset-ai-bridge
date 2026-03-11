# Extractor Implementation Guide

How to implement a new asset extractor. Read `uassetapi-notes.md` first.

---

## Role of an Extractor

An extractor takes a parsed `UAsset` and returns a `SemanticAsset` — the internal model that `Schema` will serialize to AI JSON.

Extractors own the hardest logic in the project: interpreting UE's raw export graph into something meaningful.

---

## IAssetExtractor Interface

```csharp
interface IAssetExtractor
{
    bool CanHandle(UAsset asset);
    SemanticAsset Extract(UAsset asset);
}
```

`CanHandle` must be fast (no deep parsing). Check the primary export's class name only.

`Extract` may throw. The CLI layer catches and wraps in error JSON.

---

## Extractor Registration

Extractors are tried in priority order. First match wins.

```csharp
static readonly IAssetExtractor[] Extractors =
[
    new WidgetBlueprintExtractor(),
    new BlueprintExtractor(),
    new DataTableExtractor(),
    new DataAssetExtractor(),
    new FallbackExtractor(),   // always last, always matches
];
```

Add new extractors before `FallbackExtractor`.

---

## SemanticAsset Model

```csharp
class SemanticAsset
{
    public string AssetType { get; init; }
    public string Name      { get; init; }
    public object Content   { get; init; }  // type-specific payload
}
```

`Content` is a plain C# object that will be serialized to JSON. Use anonymous objects or dedicated model classes — keep them free of UAssetAPI types.

---

## Implementing CanHandle

```csharp
public bool CanHandle(UAsset asset)
{
    if (asset.Exports.Count == 0) return false;
    var primary = asset.Exports[0];
    if (!primary.ClassIndex.IsImport()) return false;
    var className = asset.Imports[-primary.ClassIndex.Index - 1].ObjectName.Value.Value;
    return className == "WidgetBlueprint";
}
```

---

## Implementing a Simple Extractor (DataAsset example)

DataAsset is the simplest case: one export, flat property bag.

```csharp
public SemanticAsset Extract(UAsset asset)
{
    var export = asset.Exports
        .OfType<NormalExport>()
        .First(); // primary export

    var fields = new Dictionary<string, object?>();
    foreach (var prop in export.Data)
    {
        string key = prop.Name.Value.Value;
        fields[key] = ExtractScalar(prop);
    }

    return new SemanticAsset
    {
        AssetType = "DataAsset",
        Name = Path.GetFileNameWithoutExtension(asset.FilePath),
        Content = new { fields }
    };
}

static object? ExtractScalar(PropertyData prop) => prop switch
{
    StrPropertyData s  => s.Value,
    TextPropertyData t => t.Value?.Value,
    FloatPropertyData f => f.Value,
    IntPropertyData i   => i.Value,
    BoolPropertyData b  => b.Value,
    EnumPropertyData e  => e.Value.Value.Value,
    _ => null  // unsupported type: omit
};
```

Unsupported property types return `null` and are omitted from output. Do not throw on unknown types.

---

## Implementing a Hierarchical Extractor (WidgetBlueprint)

Widget extraction is recursive. The general pattern:

```csharp
public SemanticAsset Extract(UAsset asset)
{
    var widgetTree = asset.Exports
        .FirstOrDefault(e => e.ObjectName.Value.Value == "WidgetTree")
        as NormalExport
        ?? throw new InvalidOperationException("WidgetTree not found");

    var rootProp = widgetTree.Data
        .OfType<ObjectPropertyData>()
        .FirstOrDefault(p => p.Name.Value.Value == "RootWidget");

    var rootNode = rootProp != null
        ? ExtractWidget(asset, rootProp.Value)
        : null;

    return new SemanticAsset
    {
        AssetType = "WidgetBlueprint",
        Name = Path.GetFileNameWithoutExtension(asset.FilePath),
        Content = new { root = rootNode }
    };
}

WidgetNode? ExtractWidget(UAsset asset, FPackageIndex index)
{
    if (!index.IsExport()) return null;
    var export = asset.Exports[index.Index - 1] as NormalExport;
    if (export == null) return null;

    string type = ResolveClassName(asset, export.ClassIndex);
    string name = export.ObjectName.Value.Value;
    var properties = ExtractWidgetProperties(export);
    var children = ExtractChildren(asset, export);

    return new WidgetNode { Type = type, Name = name, Properties = properties, Children = children };
}
```

> **Widget hierarchy uses slot indirection, verified on real UE4.27 assets.** Panel widgets (CanvasPanel, Overlay, WrapBox, etc.) own slot exports via OuterIndex. Each slot export has a `Content` property pointing to the child widget. Single-child widgets (Button, SizeBox) use a `Content` property directly, no slot export.

### Widget Child Extraction Strategy

```
For each widget export:
  1. Find all slot exports where outer == this widget's 1-based index
  2. For each slot, read Content (ObjectPropertyData) → child export
  3. If no slots found, check for direct Content property (single-child widgets)
  4. If neither, children = []
```

Slot class → panel class mapping (verified):

| Slot class | Parent panel |
|---|---|
| `CanvasPanelSlot` | `CanvasPanel` |
| `OverlaySlot` | `Overlay` |
| `WrapBoxSlot` | `WrapBox` |
| `SizeBoxSlot` | `SizeBox` |
| `WidgetSwitcherSlot` | `WidgetSwitcher` |

Add to this table as new widget types are encountered.

---

## FallbackExtractor

Always the last in the chain. Never throws. Returns minimal info.

```csharp
public bool CanHandle(UAsset asset) => true;

public SemanticAsset Extract(UAsset asset) => new SemanticAsset
{
    AssetType = "Unknown",
    Name = Path.GetFileNameWithoutExtension(asset.FilePath),
    Content = new { exportCount = asset.Exports.Count }
};
```

---

## Testing an Extractor

1. Collect a real `.uasset` file of the target type from a UE 4.27 project
2. Run `dump-json` and inspect output manually
3. Verify: no numeric indices in output, hierarchy is correct, names match UE editor display
4. Run `dump-json` twice on the same file — output must be byte-identical (determinism check)

Place test assets in `tests/assets/` (do not commit large binary files — use small representative samples).

---

## Adding a New Asset Type Checklist

- [ ] Create `Extractors/<Type>Extractor.cs` implementing `IAssetExtractor`
- [ ] Register before `FallbackExtractor` in the extractor list
- [ ] Define the schema in `ai_schema.md` before writing code
- [ ] Add the type to the supported types table in `ai_schema.md`
- [ ] Verify deterministic output with a real asset
